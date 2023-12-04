using System.Runtime.InteropServices;

namespace ThingsDB
{
    internal class Package
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Header
        {
            readonly byte tp;
            readonly byte checkBit;
            readonly ushort pid;
            readonly uint length;
        }

        public enum Type : byte
        {
            one = 0x0,
            two = 0x1,
            three = 0x2,
        }

        public class PackageException : Exception { }
        public class UnknownType : PackageException { }
        public class InvalidCheckBit : PackageException { }
        public class SizeMismatch : PackageException { }

        public readonly Type Tp;
        public readonly byte CheckBit;
        public readonly ushort Pid;
        public readonly uint Length;

        private int size;  // size actually written in data; if less than length the package is not complete
        private byte[] data;

        public Package(byte[] header, int offset)
        {
            try
            {
                Tp = (Type)header[offset];
            }
            catch (Exception)
            {
                throw new UnknownType();
            }
            CheckBit = (byte)header[offset+1];
            Pid = (ushort)header[offset+2];
            Length = (uint)header[offset+4];

            if ((byte)Tp != ~CheckBit)
            {
                throw new InvalidCheckBit();
            }
            size = 0;
            data = new byte[Length];
        }

        public Package(Type tp, byte[] data)
        {

        }

        public void SetData(byte[] data)
        {
            if (data.Length != Length)
            {
                throw new SizeMismatch();
            }
            size = data.Length;
            this.data = data;
        }

        public int CopyData(byte[] data, int offset, int length)
        {
            if (size + length > Length)
            {
                length = (int)Length - size;
            }
            Array.Copy(data, offset, this.data, size, length);
            size += length;
            return length;
        }

        public bool IsComplete()
        {
            return size == Length;
        }
    }
}
