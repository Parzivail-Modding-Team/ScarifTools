using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Ionic.Zlib;

namespace Substrate.Core
{
    public class ChunkFile : NBTFile
    {
        public ChunkFile (string path)
            : base(path)
        {
        }

        public ChunkFile (string path, int cx, int cz)
            : base("")
        {
            var cx64 = Base36.Encode(cx);
            var cz64 = Base36.Encode(cz);
            var file = "c." + cx64 + "." + cz64 + ".dat";

            while (cx < 0) {
                cx += (64 * 64);
            }
            while (cz < 0) {
                cz += (64 * 64);
            }

            var dir1 = Base36.Encode(cx % 64);
            var dir2 = Base36.Encode(cz % 64);

            FileName = Path.Combine(path, dir1);
            if (!Directory.Exists(FileName)) {
                Directory.CreateDirectory(FileName);
            }

            FileName = Path.Combine(FileName, dir2);
            if (!Directory.Exists(FileName)) {
                Directory.CreateDirectory(FileName);
            }

            FileName = Path.Combine(FileName, file);
        }
    }
}
