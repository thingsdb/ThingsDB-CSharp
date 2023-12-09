using MessagePack;

namespace ThingsDB
{
    internal class Package
    {
        static public readonly int HeaderSize = 8;

        private readonly byte tp;
        private readonly byte checkBit;
        private readonly ushort pid;
        private readonly uint length;

        private readonly byte[] data;
        private int size;  // size actually written in data; if less than length the package is not complete

        public Package(byte[] header)
        {
            tp = header[6];
            checkBit = header[7];

            if (BitConverter.IsLittleEndian)
            {
                length = header[0];
                pid = header[4];
            }
            else
            {
                byte[] tmp = new byte[6];
                Array.Copy(header, 0, tmp, 0, 6);
                Array.Reverse(tmp);
                length = tmp[2];
                pid = tmp[0];
            }

            if (tp != (checkBit ^ 0xff))
            {
                throw new InvalidCheckBit();
            }
            size = 0;
            data = new byte[length];
        }
        public Package(PackageType tp, ushort pid, byte[] data)
        {
            this.tp = (byte)tp;
            checkBit = (byte)(this.tp ^ 0xff);
            this.pid = pid;
            length = (uint)data.Length;
            this.data = data;
        }
        public int CopyData(byte[] data, int offset, int n)
        {
            if ((size + n) > length)
            {
                n = (int)length - size;
            }
            Array.Copy(data, offset, this.data, size, n);
            size += n;
            return n;
        }
        public bool IsComplete()
        {
            return size == length;
        }
        public PackageType Tp() { return (PackageType)tp; }
        public int Pid() { return pid; }
        public int Length() { return (int)length; }
        public int Size() { return HeaderSize + (int)length; }
        public byte[] Data() { return data; }
        public byte[] GetBytes()
        {
            byte[] bytes = new byte[HeaderSize + length];
            Pack(bytes, 0, length);
            Pack(bytes, 4, pid);
            bytes[6] = tp;
            bytes[7] = checkBit;
            Array.Copy(data, 0, bytes, 8, length);
            return bytes;
        }
        static private void Pack(byte[] destination, int offset, ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, destination, offset, bytes.Length);
        }
        static private void Pack(byte[] destination, int offset, uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, destination, offset, bytes.Length);
        }
        public void RaiseOnErr()
        {
            var tp = Tp();
            if (tp == PackageType.ResError)
            {
                ErrorType err = MessagePackSerializer.Deserialize<ErrorType>(data);
                throw err.Code switch
                {
                    -64 => new CancelledException(err.Msg, err.Code),
                    -63 => new OperationException(err.Msg, err.Code),
                    -62 => new NumArgumentsException(err.Msg, err.Code),
                    -61 => new TypeError(err.Msg, err.Code),
                    -60 => new ValueError(err.Msg, err.Code),
                    -59 => new OverflowException(err.Msg, err.Code),
                    -58 => new ZeroDivException(err.Msg, err.Code),
                    -57 => new MaxQuotaException(err.Msg, err.Code),
                    -56 => new AuthError(err.Msg, err.Code),
                    -55 => new ForbiddenException(err.Msg, err.Code),
                    -54 => new LookupError(err.Msg, err.Code),
                    -53 => new BadDataException(err.Msg, err.Code),
                    -52 => new SyntaxError(err.Msg, err.Code),
                    -51 => new NodeError(err.Msg, err.Code),
                    -50 => new AssertError(err.Msg, err.Code),
                    -6 => new ResultTooLarge(err.Msg, err.Code),
                    -5 => new RequestTimeout(err.Msg, err.Code),
                    -4 => new RequestCancel(err.Msg, err.Code),
                    -3 => new WriteUVException(err.Msg, err.Code),
                    -2 => new MemoryException(err.Msg, err.Code),
                    -1 => new InternalException(err.Msg, err.Code),
                    -0 => new SuccessException(err.Msg, err.Code),
                    _ => new TiError(err.Msg, err.Code),
                };
            }
        }
    }
}
