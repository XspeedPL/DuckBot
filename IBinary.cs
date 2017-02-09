using System.IO;

namespace DuckBot
{
    interface IBinary
    {
        void Load(BinaryReader br);

        void Save(BinaryWriter bw);
    }
}
