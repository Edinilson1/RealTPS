using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using RealTPS.Core.Materials;
using RealTPS.Core.Shield;

namespace RtpsTool
{
    /// <summary>
    /// Renders the shield map of a few fixture states to PNG files, with the very code the game window uses
    /// (<see cref="ShieldMapRenderer"/>). It lets the picture be checked without the game; it says nothing
    /// about how the window looks or behaves in the game.
    /// </summary>
    internal static class MapRender
    {
        private const int Size = 256;

        public static int Run(Repository repo, string outDir)
        {
            MaterialDefinition ablator;
            if (!repo.Catalogue.TryGet("avcoat-5026-39", out ablator))
            {
                ablator = repo.Catalogue.Materials.First(m => m.Ablation != null);
            }
            Directory.CreateDirectory(outDir);
            var written = new List<string>();

            // 1. Healthy, uniform layer at 80 %.
            ShieldState healthy = Stocked(ablator, 0.8);
            written.Add(Save(outDir, "01-healthy-reserve", ablator, healthy, ShieldView.Reserve, false, -1));
            written.Add(Save(outDir, "02-healthy-reserve-grid", ablator, healthy, ShieldView.Reserve, true, -1));

            // 2. A tilted entry: the solver itself produces the uneven consumption.
            ShieldState tilted = Stocked(ablator, 1.0);
            var result = new ShieldStepResult(tilted.Count);
            for (int i = 0; i < 700; i++)
            {
                ShieldSolver.Step(ablator, tilted, new ShieldStepInput
                {
                    Dt = 0.05,
                    SkinTemp = 2000.0,
                    InternalTemp = 300.0,
                    ExposedSkinHeatCapacity = 1.0e9,
                    ConvectivePower = 4.0e6,
                    Flow = new Incidence { Defined = true, Facing = 0.94, Tilt = 0.34, Azimuth = Math.PI / 2.0 },
                    Settings = new ShieldSettings { CellVariation = 0.0 }
                }, result);
            }
            written.Add(Save(outDir, "03-tilted-entry-reserve", ablator, tilted, ShieldView.Reserve, false, -1));
            written.Add(Save(outDir, "04-tilted-entry-reserve-grid", ablator, tilted, ShieldView.Reserve, true, -1));
            written.Add(Save(outDir, "05-tilted-entry-bond-temperature", ablator, tilted, ShieldView.SubstrateTemp, false, -1));
            written.Add(Save(outDir, "06-tilted-entry-received", ablator, tilted, ShieldView.Received, false, -1));

            // 3. One exposed cell, one selected.
            ShieldState breached = Stocked(ablator, 0.7);
            breached.Inventory[17] = 0.0;
            written.Add(Save(outDir, "07-one-exposed-cell", ablator, breached, ShieldView.Reserve, false, 3));

            // 4. An intact cell surrounded by exposed ones.
            ShieldState island = Stocked(ablator, 0.7);
            foreach (int cell in new[] { 0, 1, 2, 8, 10, 16, 17, 18 })
            {
                island.Inventory[cell] = 0.0;
            }
            written.Add(Save(outDir, "08-intact-cell-among-exposed", ablator, island, ShieldView.Reserve, false, -1));

            // 5. Every alert at once: critical integrity, low reserve, bond at risk, exposed.
            ShieldState alerts = Stocked(ablator, 0.9);
            alerts.ThermalInitialised = true;
            for (int i = 0; i < alerts.Count; i++)
            {
                alerts.SubstrateTemp[i] = 330.0 + 4.0 * i;
            }
            alerts.Integrity[4] = 0.1;
            alerts.Inventory[12] = alerts.Capacity[12] * 0.05;
            alerts.SubstrateTemp[20] = alerts.Construction.BondLimit(ablator) - 40.0;
            alerts.Inventory[22] = 0.0;
            written.Add(Save(outDir, "09-all-alerts-integrity", ablator, alerts, ShieldView.Integrity, false, -1));
            written.Add(Save(outDir, "10-all-alerts-bond-margin", ablator, alerts, ShieldView.BondMargin, false, -1));

            // 6. A legacy single-cell shield.
            MaterialDefinition legacy = LegacyProfile.Create(1.27, 200.0, 0.4, 3300.0);
            ShieldState single = ShieldState.Uniform(ShieldLayout.Single(), new ShieldConstruction { Area = 1.27 }, 120.0, 200.0, 80.0);
            written.Add(Save(outDir, "11-legacy-single-cell", legacy, single, ShieldView.Reserve, false, -1));

            foreach (string file in written)
            {
                Console.WriteLine("written     " + file);
            }
            return 0;
        }

        private static ShieldState Stocked(MaterialDefinition m, double fill)
        {
            const double area = 1.27;
            double capacity = m.ConsumableArealDensity * area;
            var construction = new ShieldConstruction { Area = area, LayerThickness = m.DesignThickness };
            return ShieldState.Uniform(new ShieldLayout(8, 2, 2.0), construction, capacity * fill, capacity, 0.0);
        }

        private static string Save(string dir, string name, MaterialDefinition m, ShieldState state, ShieldView view, bool grid, int selected)
        {
            var data = new ShieldMapData(ShieldLayout.MaxCells);
            data.Fill(m, state, new ShieldSettings { CellVariation = 0.0 });
            var cells = new int[Size * Size];
            var rgba = new byte[Size * Size * 4];
            ShieldMapRenderer.CellMap(state.Layout, Size, cells);
            ShieldMapRenderer.Render(data, view, MapScale.For(view, state.Construction.BondLimit(m)), cells, Size, grid, selected, rgba);
            // A dark backdrop, as in the game window, so that transparent texels are not white.
            for (int p = 0; p < Size * Size; p++)
            {
                if (rgba[p * 4 + 3] == 0)
                {
                    rgba[p * 4] = 28;
                    rgba[p * 4 + 1] = 30;
                    rgba[p * 4 + 2] = 34;
                    rgba[p * 4 + 3] = 255;
                }
            }
            string path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, Png(rgba, Size, Size));
            return path;
        }

        /// <summary>Minimal PNG encoder: 8-bit RGBA, no filtering.</summary>
        private static byte[] Png(byte[] rgba, int width, int height)
        {
            using (var output = new MemoryStream())
            {
                output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
                var header = new byte[13];
                WriteInt(header, 0, width);
                WriteInt(header, 4, height);
                header[8] = 8;      // bit depth
                header[9] = 6;      // colour type RGBA
                Chunk(output, "IHDR", header);

                byte[] raw = new byte[height * (width * 4 + 1)];
                for (int y = 0; y < height; y++)
                {
                    Buffer.BlockCopy(rgba, y * width * 4, raw, y * (width * 4 + 1) + 1, width * 4);
                }
                using (var compressed = new MemoryStream())
                {
                    using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true))
                    {
                        zlib.Write(raw, 0, raw.Length);
                    }
                    Chunk(output, "IDAT", compressed.ToArray());
                }
                Chunk(output, "IEND", new byte[0]);
                return output.ToArray();
            }
        }

        private static void Chunk(Stream output, string type, byte[] data)
        {
            var length = new byte[4];
            WriteInt(length, 0, data.Length);
            output.Write(length, 0, 4);
            byte[] name = System.Text.Encoding.ASCII.GetBytes(type);
            output.Write(name, 0, 4);
            output.Write(data, 0, data.Length);
            uint crc = Crc(name, 0xFFFFFFFFu);
            crc = Crc(data, crc) ^ 0xFFFFFFFFu;
            var tail = new byte[4];
            WriteInt(tail, 0, unchecked((int)crc));
            output.Write(tail, 0, 4);
        }

        private static uint Crc(byte[] data, uint crc)
        {
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int k = 0; k < 8; k++)
                {
                    crc = (crc & 1u) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
                }
            }
            return crc;
        }

        private static void WriteInt(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }
    }
}
