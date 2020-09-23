//MIT License
//
//Copyright (c) 2017 Tommaso Belluzzo
//
//Permission is hereby granted, free of charge, to any person obtaining a copy of
//this software and associated documentation files (the "Software"), to deal in
//the Software without restriction, including without limitation the rights to
//use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
//of the Software, and to permit persons to whom the Software is furnished to do
//so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

//https://github.com/TommasoBelluzzo/FastHashes

namespace FastHashes
{
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Linq;

    /// <summary>Represents the base class from which all the FarmHash implementations with more than 32 bits of output must derive. This class is abstract.</summary>
    abstract class FarmHashG32 : Hash
    {
        /// <summary>Represents the K0 value. This field is constant.</summary>
        protected const ulong K0 = 0xC3A5C85C97CB3127ul;

        /// <summary>Represents the K1 value. This field is constant.</summary>
        protected const ulong K1 = 0xB492B66FBE98F273ul;

        /// <summary>Represents the K2 value. This field is constant.</summary>
        protected const ulong K2 = 0x9AE16A3B2F90404Ful;

        /// <summary>Represents the K3 value. This field is constant.</summary>
        protected const ulong K3 = 0x9DDFEA08EB382D69ul;

        /// <summary>Represents the seeds used by the hashing algorithm. This field is read-only.</summary>
        protected readonly ReadOnlyCollection<ulong> MSeeds;

        /// <summary>Gets the seeds used by the hashing algorithm.</summary>
        /// <value>A <see cref="System.Collections.ObjectModel.ReadOnlyCollection{T}"/> containing <c>0</c> or <c>2</c> <see cref="T:System.UInt64"/> values.</value>
        public ReadOnlyCollection<ulong> Seeds => MSeeds;

        /// <summary>Represents the base constructor without seeds used by derived classes.</summary>
        protected FarmHashG32()
        {
            MSeeds = new ReadOnlyCollection<ulong>(new ulong[0]);
        }

        /// <summary>Represents the base constructor with two seeds used by derived classes.</summary>
        /// <param name="seed1">The first <see cref="T:System.UInt64"/> seed used by the hashing algorithm.</param>
        /// <param name="seed2">The second <see cref="T:System.UInt64"/> seed used by the hashing algorithm.</param>
        protected FarmHashG32(ulong seed1, ulong seed2)
        {
            MSeeds = new ReadOnlyCollection<ulong>(new[] { seed1, seed2 });
        }

        /// <summary>Represents an auxiliary hashing function used by derived classes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static ulong HashLength16(ulong v1, ulong v2, ulong m)
        {
            ulong a = ShiftMix((v1 ^ v2) * m);
            return ShiftMix((v2 ^ a) * m) * m;
        }

        /// <summary>Represents an auxiliary hashing function used by derived classes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static ulong ShiftMix(ulong v)
        {

            return v ^ (v >> 47);
        }

        /// <summary>Represents an auxiliary hashing function used by derived classes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe void Update(ref ulong x, ref ulong y, ref ulong z, byte* pointer, ulong v0, ulong v1, ulong w0, ulong w1, ulong m, ulong c)
        {
            x = RotateRight(x + y + v0 + Fetch64(pointer + 8), 37) * m;
            y = RotateRight(y + v1 + Fetch64(pointer + 48), 42) * m;
            x ^= w1 * c;
            y += (v0 * c) + Fetch64(pointer + 40);
            z = RotateRight(z + w0, 33) * m;
        }

        /// <summary>Represents an auxiliary hashing function used by derived classes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe void HashWeak32(out ulong v1, out ulong v2, byte* pointer, ulong v3, ulong v4)
        {
            ulong w = Fetch64(pointer);
            ulong x = Fetch64(pointer + 8);
            ulong y = Fetch64(pointer + 16);
            ulong z = Fetch64(pointer + 24);

            v3 += w;
            v4 = RotateRight(v4 + v3 + z, 21);

            ulong c = v3;

            v3 += x;
            v3 += y;
            v4 += RotateRight(v3, 44);

            v1 = v3 + z;
            v2 = v4 + c;
        }
    }

    /// <summary>Represents the FarmHash128 implementation. This class cannot be derived.</summary>
    sealed class FarmHash128 : FarmHashG32
    {
        /// <inheritdoc/>
        public override int Length => 128;

        /// <summary>Initializes a new instance without seeds.</summary>
        public FarmHash128() { }

        /// <summary>Initializes a new instance using the specified <see cref="T:System.UInt64"/> value for both seeds.</summary>
        /// <param name="seed">The <see cref="T:System.UInt64"/> seed used by the hashing algorithm.</param>
        public FarmHash128(ulong seed) : base(seed, seed) { }

        /// <summary>Initializes a new instance using the specified <see cref="T:System.UInt64"/> seeds.</summary>
        /// <param name="seed1">The first <see cref="T:System.UInt64"/> seed used by the hashing algorithm.</param>
        /// <param name="seed2">The second <see cref="T:System.UInt64"/> seed used by the hashing algorithm.</param>
        public FarmHash128(ulong seed1, ulong seed2) : base(seed1, seed2) { }

        /// <inheritdoc/>
        protected override byte[] ComputeHashInternal(byte[] buffer, int offset, int count)
        {
            ulong seed1, seed2;
            ulong hash1, hash2;

            if (count == 0)
            {
                if (MSeeds.Count == 0)
                {
                    seed1 = K0;
                    seed2 = K1;
                }
                else
                {
                    seed1 = MSeeds[0];
                    seed2 = MSeeds[1];
                }

                Hash0(out hash1, out hash2, seed1, seed2);

                goto Finalize;
            }

            unsafe
            {
                fixed (byte* pin = &buffer[offset])
                {
                    byte* pointer = pin;

                    if (MSeeds.Count == 0)
                    {
                        if (count >= 16)
                        {
                            seed1 = Fetch64(pointer);
                            seed2 = Fetch64(pointer + 8) + K0;

                            pointer += 16;
                            count -= 16;
                        }
                        else
                        {
                            seed1 = K0;
                            seed2 = K1;
                        }
                    }
                    else
                    {
                        seed1 = MSeeds[0];
                        seed2 = MSeeds[1];
                    }

                    if (count <= 3)
                        Hash1To3(out hash1, out hash2, pointer, count, seed1, seed2);
                    else if (count <= 7)
                        Hash4To7(out hash1, out hash2, pointer, count, seed1, seed2);
                    else if (count <= 16)
                        Hash8To16(out hash1, out hash2, pointer, count, seed1, seed2);
                    else if (count <= 127)
                        Hash17To127(out hash1, out hash2, pointer, count, seed1, seed2);
                    else
                        Hash128ToEnd(out hash1, out hash2, pointer, count, seed1, seed2);
                }
            }

        Finalize:

            byte[] result = new byte[16];

            unsafe
            {
                fixed (byte* pin = result)
                {
                    ulong* pointer = (ulong*)pin;
                    pointer[0] = hash1;
                    pointer[1] = hash2;
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Hash0(out ulong hash1, out ulong hash2, ulong seed1, ulong seed2)
        {
            ulong a = ShiftMix(seed1 * K1) * K1;
            ulong b = seed2;
            ulong c = (b * K1) + K2;
            ulong d = ShiftMix(a + c);
            ulong e = HashLength16(a, c, K3);
            ulong f = HashLength16(d, b, K3);

            hash1 = e ^ f;
            hash2 = HashLength16(f, e, K3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void Hash1To3(out ulong hash1, out ulong hash2, byte* pointer, int length, ulong seed1, ulong seed2)
        {
            byte v0 = pointer[0];
            byte v1 = pointer[length >> 1];
            byte v2 = pointer[length - 1];
            uint v3 = v0 + ((uint)v1 << 8);
            uint v4 = (uint)length + ((uint)v2 << 2);
            ulong k = ShiftMix(v3 * K2 ^ v4 * K0) * K2;

            ulong a = ShiftMix(seed1 * K1) * K1;
            ulong b = seed2;
            ulong c = (b * K1) + k;
            ulong d = ShiftMix(a + c);
            ulong e = HashLength16(a, c, K3);
            ulong f = HashLength16(d, b, K3);

            hash1 = e ^ f;
            hash2 = HashLength16(f, e, K3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void Hash4To7(out ulong hash1, out ulong hash2, byte* pointer, int length, ulong seed1, ulong seed2)
        {
            ulong lengthUnsigned = (ulong)length;

            ulong v0 = K2 + (lengthUnsigned * 2ul);
            ulong v1 = lengthUnsigned + ((ulong)Fetch32(pointer) << 3);
            ulong v2 = Fetch32(pointer + length - 4);
            ulong k = HashLength16(v1, v2, v0);

            ulong a = ShiftMix(seed1 * K1) * K1;
            ulong b = seed2;
            ulong c = (b * K1) + k;
            ulong d = ShiftMix(a + c);
            ulong e = HashLength16(a, c, K3);
            ulong f = HashLength16(d, b, K3);

            hash1 = e ^ f;
            hash2 = HashLength16(f, e, K3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void Hash8To16(out ulong hash1, out ulong hash2, byte* pointer, int length, ulong seed1, ulong seed2)
        {
            ulong lengthUnsigned = (ulong)length;

            ulong v0 = K2 + (lengthUnsigned * 2ul);
            ulong v1 = Fetch64(pointer) + K2;
            ulong v2 = Fetch64(pointer + length - 8);
            ulong v3 = (RotateRight(v2, 37) * v0) + v1;
            ulong v4 = (RotateRight(v1, 25) + v2) * v0;
            ulong k = HashLength16(v3, v4, v0);

            ulong a = ShiftMix(seed1 * K1) * K1;
            ulong b = seed2;
            ulong c = (b * K1) + k;
            ulong d = ShiftMix(a + Fetch64(pointer));
            ulong e = HashLength16(a, c, K3);
            ulong f = HashLength16(d, b, K3);

            hash1 = e ^ f;
            hash2 = HashLength16(f, e, K3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void Hash17To127(out ulong hash1, out ulong hash2, byte* pointer, int length, ulong seed1, ulong seed2)
        {
            ulong a = seed1;
            ulong b = seed2;
            ulong c = HashLength16(Fetch64(pointer + length - 8) + K1, a, K3);
            ulong d = HashLength16(b + (ulong)length, c + Fetch64(pointer + length - 16), K3);

            a += d;

            int remainder = length - 16;

            do
            {
                a ^= ShiftMix(Fetch64(pointer) * K1) * K1;
                a *= K1;
                b ^= a;
                c ^= ShiftMix(Fetch64(pointer + 8) * K1) * K1;
                c *= K1;
                d ^= c;

                pointer += 16;
                remainder -= 16;
            }
            while (remainder > 0);

            ulong e = HashLength16(a, c, K3);
            ulong f = HashLength16(d, b, K3);

            hash1 = e ^ f;
            hash2 = HashLength16(f, e, K3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void Hash128ToEnd(out ulong hash1, out ulong hash2, byte* pointer, int length, ulong seed1, ulong seed2)
        {
            ulong x = seed1;
            ulong y = seed2;
            ulong z = (ulong)length * K1;

            ulong v0 = (RotateRight(y ^ K1, 49) * K1) + Fetch64(pointer);
            ulong v1 = (RotateRight(v0, 42) * K1) + Fetch64(pointer + 8);
            ulong w0 = (RotateRight(y + z, 35) * K1) + x;
            ulong w1 = RotateRight(x + Fetch64(pointer + 88), 53) * K1;

            do
            {
                for (int i = 0; i < 2; ++i)
                {
                    Update(ref x, ref y, ref z, pointer, v0, v1, w0, w1, K1, 1ul);

                    HashWeak32(out v0, out v1, pointer, v1 * K1, x + w0);
                    HashWeak32(out w0, out w1, pointer + 32, z + w1, y + Fetch64(pointer + 16));
                    Swap(ref z, ref x);

                    pointer += 64;
                }

                length -= 128;
            }
            while (length >= 128);

            x += RotateRight(v0 + z, 49) * K0;
            y = (y * K0) + RotateRight(w1, 37);
            z = (z * K0) + RotateRight(w0, 27);
            w0 *= 9ul;
            v0 *= K0;

            int t = 0;

            while (t < length)
            {
                t += 32;

                int lengthDiff = length - t;

                y = (RotateRight(x + y, 42) * K0) + v1;
                w0 += Fetch64(pointer + lengthDiff + 16);
                x = (x * K0) + w0;
                z += w1 + Fetch64(pointer + lengthDiff);
                w1 += v0;

                HashWeak32(out v0, out v1, pointer + lengthDiff, v0 + z, v1);
                v0 *= K0;
            }

            x = HashLength16(x, v0, K3);
            y = HashLength16(y + z, w0, K3);

            hash1 = HashLength16(x + v1, w1, K3) + y;
            hash2 = HashLength16(x + w1, y + v1, K3);
        }
    }

    /// <summary>Represents the base class from which all the hash algorithms must derive. This class is abstract.</summary>
    abstract class Hash
    {
        #region Members (Static)
        private static readonly Boolean s_AllowsUnalignedRead = AllowsUnalignedRead();
        private static readonly Boolean s_IsLittleEndian = BitConverter.IsLittleEndian;
        #endregion

        #region Properties (Abstract)
        /// <summary>Gets the size, in bits, of the computed hash code.</summary>
        /// <value>An <see cref="T:System.Int32"/> value, greater than or equal to <c>32</c>.</value>
        public abstract Int32 Length { get; }
        #endregion

        #region Methods
        private static Boolean AllowsUnalignedRead()
        {
            if ((new[] {"x86", "amd64"}).Contains(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"), StringComparer.OrdinalIgnoreCase))
                return true;

            ProcessStartInfo si = new ProcessStartInfo
            {
                CreateNoWindow = true,
                ErrorDialog = false,
                FileName = "uname",
                LoadUserProfile = false,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = si;
                    process.StartInfo.Arguments = "-p";

                    process.Start();

                    using (StreamReader stream = process.StandardOutput)
                    {
                        String output = stream.ReadLine();

                        if (!String.IsNullOrWhiteSpace(output))
                        {
                            output = output.Trim();

                            if ((new[] {"amd64", "i386", "x64", "x86_64"}).Contains(output, StringComparer.OrdinalIgnoreCase))
                                return true;
                        }
                    }
                }
            }
            catch { }

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = si;
                    process.StartInfo.Arguments = "-m";

                    process.Start();

                    using (StreamReader stream = process.StandardOutput)
                    {
                        String output = stream.ReadLine();

                        if (!String.IsNullOrWhiteSpace(output))
                        {
                            output = output.Trim();

                            if ((new[] {"amd64", "x64", "x86_64"}).Contains(output, StringComparer.OrdinalIgnoreCase))
                                return true;

                            if ((new Regex(@"i\d86")).IsMatch(output))
                                return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>Reads a 2-bytes unsigned integer from the specified byte pointer, without increment.</summary>
        /// <param name="pointer">The <see cref="T:System.Byte"/>* to read.</param>
        /// <returns>An <see cref="T:System.UInt16"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe UInt16 Fetch16(Byte* pointer)
        {
            UInt16 v;

            if (s_IsLittleEndian)
            {
                if (s_AllowsUnalignedRead || (((Int64)pointer & 7) == 0))
                    v = *((UInt16*)pointer);
                else
                    v = (UInt16)(pointer[0] | (pointer[1] << 8));
            }
            else
                v = (UInt16)((pointer[0] << 8) | pointer[1]);

            return v;
        }

        /// <summary>Reads a 4-bytes unsigned integer from the specified byte pointer, without increment.</summary>
        /// <param name="pointer">The <see cref="T:System.Byte"/>* to read.</param>
        /// <returns>An <see cref="T:System.UInt32"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe UInt32 Fetch32(Byte* pointer)
        {
            UInt32 v;

            if (s_IsLittleEndian)
            {
                if (s_AllowsUnalignedRead || (((Int64)pointer & 7) == 0))
                    v = *((UInt32*)pointer);
                else
                    v = (UInt32)(pointer[0] | (pointer[1] << 8) | (pointer[2] << 16) | (pointer[3] << 24));
            }
            else
                v = (UInt32)((pointer[0] << 24) | (pointer[1] << 16) | (pointer[2] << 8) | pointer[3]);

            return v;
        }

        /// <summary>Reads a 8-bytes unsigned integer from the specified byte pointer, without increment.</summary>
        /// <param name="pointer">The <see cref="T:System.Byte"/>* to read.</param>
        /// <returns>An <see cref="T:System.UInt64"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe UInt64 Fetch64(Byte* pointer)
        {
            UInt64 v;

            if (s_IsLittleEndian)
            {
                if (s_AllowsUnalignedRead || (((Int64)pointer & 7) == 0))
                    v = *((UInt64*)pointer);
                else
                    v = (UInt64)(pointer[0] | (pointer[1] << 8) | (pointer[2] << 16) | (pointer[3] << 24) | (pointer[4] << 32) | (pointer[5] << 40) | (pointer[6] << 48) | (pointer[7] << 56));
            }
            else
                v = (UInt64)((pointer[0] << 56) | (pointer[1] << 48) | (pointer[2] << 40) | (pointer[3] << 32) | (pointer[4] << 24) | (pointer[5] << 16) | (pointer[6] << 8) | pointer[7]);

            return v;
        }

        /// <summary>Reads a 2-bytes unsigned integer from the specified byte pointer, with increment.</summary>
        /// <param name="pointer">The <see cref="T:System.Byte"/>* to read.</param>
        /// <returns>An <see cref="T:System.UInt16"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe UInt16 Read16(ref Byte* pointer)
        {
            UInt16 v;

            if (s_IsLittleEndian)
            {
                if (s_AllowsUnalignedRead || (((Int64)pointer & 7) == 0))
                    v = *((UInt16*)pointer);
                else
                    v = (UInt16)(pointer[0] | (pointer[1] << 8));
            }
            else
                v = (UInt16)((pointer[0] << 8) | pointer[1]);

            pointer += 2;

            return v;
        }

        /// <summary>Reads a 4-bytes unsigned integer from the specified byte pointer, with increment.</summary>
        /// <param name="pointer">The <see cref="T:System.Byte"/>* to read.</param>
        /// <returns>An <see cref="T:System.UInt32"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe UInt32 Read32(ref Byte* pointer)
        {
            UInt32 v;

            if (s_IsLittleEndian)
            {
                if (s_AllowsUnalignedRead || (((Int64)pointer & 7) == 0))
                    v = *((UInt32*)pointer);
                else
                    v = (UInt32)(pointer[0] | (pointer[1] << 8) | (pointer[2] << 16) | (pointer[3] << 24));
            }
            else
                v = (UInt32)((pointer[0] << 24) | (pointer[1] << 16) | (pointer[2] << 8) | pointer[3]);

            pointer += 4;

            return v;
        }

        /// <summary>Reads a 8-bytes unsigned integer from the specified byte pointer, with increment.</summary>
        /// <param name="pointer">The <see cref="T:System.Byte"/>* to read.</param>
        /// <returns>An <see cref="T:System.UInt64"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe UInt64 Read64(ref Byte* pointer)
        {
            UInt64 v;

            if (s_IsLittleEndian)
            {
                if (s_AllowsUnalignedRead || (((Int64)pointer & 7) == 0))
                    v = *((UInt64*)pointer);
                else
                    v = (UInt64)(pointer[0] | (pointer[1] << 8) | (pointer[2] << 16) | (pointer[3] << 24) | (pointer[4] << 32) | (pointer[5] << 40) | (pointer[6] << 48) | (pointer[7] << 56));
            }
            else
                v = (UInt64)((pointer[0] << 56) | (pointer[1] << 48) | (pointer[2] << 40) | (pointer[3] << 32) | (pointer[4] << 24) | (pointer[5] << 16) | (pointer[6] << 8) | pointer[7]);

            pointer += 8;

            return v;
        }

        /// <summary>Rotates a 4-bytes unsigned integer left by the specified number of bits.</summary>
        /// <param name="value">The <see cref="T:System.UInt32"/> to rotate.</param>
        /// <param name="rotation">The number of bits to rotate.</param>
        /// <returns>An <see cref="T:System.UInt32"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static UInt32 RotateLeft(UInt32 value, Int32 rotation)
        {
            rotation &= 0x1F;
            return (value << rotation) | (value >> (32 - rotation));
        }

        /// <summary>Rotates a 8-bytes unsigned integer left by the specified number of bits.</summary>
        /// <param name="value">The <see cref="T:System.UInt64"/> to rotate.</param>
        /// <param name="rotation">The number of bits to rotate.</param>
        /// <returns>An <see cref="T:System.UInt64"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static UInt64 RotateLeft(UInt64 value, Int32 rotation)
        {
            rotation &= 0x3F;
            return (value << rotation) | (value >> (64 - rotation));
        }

        /// <summary>Rotates a 4-bytes unsigned integer right by the specified number of bits.</summary>
        /// <param name="value">The <see cref="T:System.UInt32"/> to rotate.</param>
        /// <param name="rotation">The number of bits to rotate.</param>
        /// <returns>An <see cref="T:System.UInt32"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static UInt32 RotateRight(UInt32 value, Int32 rotation)
        {
            rotation &= 0x1F;
            return (value >> rotation) | (value << (32 - rotation));
        }

        /// <summary>Rotates a 8-bytes unsigned integer right by the specified number of bits.</summary>
        /// <param name="value">The <see cref="T:System.UInt64"/> to rotate.</param>
        /// <param name="rotation">The number of bits to rotate.</param>
        /// <returns>An <see cref="T:System.UInt64"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static UInt64 RotateRight(UInt64 value, Int32 rotation)
        {
            rotation &= 0x3F;
            return (value >> rotation) | (value << (64 - rotation));
        }

        /// <summary>Swaps the value of two 2-bytes unsigned integers.</summary>
        /// <param name="value1">The first <see cref="T:System.UInt16"/>, whose value is assigned to the second one.</param>
        /// <param name="value2">The second <see cref="T:System.UInt16"/>, whose value is assigned to the first one.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void Swap(ref UInt16 value1, ref UInt16 value2)
        {
            UInt16 tmp = value1;
            value1 = value2;
            value2 = tmp;
        }

        /// <summary>Swaps the value of two 4-bytes unsigned integers.</summary>
        /// <param name="value1">The first <see cref="T:System.UInt32"/>, whose value is assigned to the second one.</param>
        /// <param name="value2">The second <see cref="T:System.UInt32"/>, whose value is assigned to the first one.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void Swap(ref UInt32 value1, ref UInt32 value2)
        {
            UInt32 tmp = value1;
            value1 = value2;
            value2 = tmp;
        }

        /// <summary>Swaps the value of two 8-bytes unsigned integers.</summary>
        /// <param name="value1">The first <see cref="T:System.UInt64"/>, whose value is assigned to the second one.</param>
        /// <param name="value2">The second <see cref="T:System.UInt64"/>, whose value is assigned to the first one.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void Swap(ref UInt64 value1, ref UInt64 value2)
        {
            UInt64 tmp = value1;
            value1 = value2;
            value2 = tmp;
        }

        /// <summary>Computes the hash of the specified byte array.</summary>
        /// <param name="buffer">The <see cref="T:System.Byte"/>[] whose hash must be computed.</param>
        /// <returns>A <see cref="T:System.Byte"/>[] representing the computed hash.</returns>
        /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="buffer">buffer</paramref> is <c>null</c>.</exception>
        public Byte[] ComputeHash(Byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            return ComputeHash(buffer, 0, buffer.Length);
        }

        /// <summary>Computes the hash of the specified number of elements of a byte array, starting at the first element.</summary>
        /// <param name="buffer">The <see cref="T:System.Byte"/>[] whose hash must be computed.</param>
        /// <param name="count">The number of bytes in the array to use as data.</param>
        /// <returns>A <see cref="T:System.Byte"/>[] representing the computed hash.</returns>
        /// <exception cref="T:System.ArgumentException">Thrown when the number of bytes in <paramref name="buffer">buffer</paramref> is less than <paramref name="count">count</paramref>.</exception>
        /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="buffer">buffer</paramref> is <c>null</c>.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="count">count</paramref> is less than <c>0</c>.</exception>
        public Byte[] ComputeHash(Byte[] buffer, Int32 count)
        {
            return ComputeHash(buffer, 0, count);
        }

        /// <summary>Computes the hash of the specified region of a byte array.</summary>
        /// <param name="buffer">The <see cref="T:System.Byte"/>[] whose hash must be computed.</param>
        /// <param name="offset">The offset into the byte array from which to begin using data.</param>
        /// <param name="count">The number of bytes in the array to use as data.</param>
        /// <returns>A <see cref="T:System.Byte"/>[] representing the computed hash.</returns>
        /// <exception cref="T:System.ArgumentException">Thrown when the number of bytes in <paramref name="buffer">buffer</paramref> is less than <paramref name="offset">sourceOffset</paramref> plus <paramref name="count">count</paramref>.</exception>
        /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="buffer">buffer</paramref> is <c>null</c>.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="offset">offset</paramref> is not within the bounds of <paramref name="buffer">buffer</paramref> or when <paramref name="count">count</paramref> is less than <c>0</c>.</exception>
        public Byte[] ComputeHash(Byte[] buffer, Int32 offset, Int32 count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            Int32 bufferLength = buffer.Length;

            if ((offset < 0) || (offset >= bufferLength))
                throw new ArgumentOutOfRangeException(nameof(offset), "The offset parameter must be within the bounds of the data array.");

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "The count parameter must be greater than or equal to 0.");

            if ((offset + count) > bufferLength)
                throw new ArgumentException("The block defined by offset and count parameters must be within the bounds of the data array.");

            return ComputeHashInternal(buffer, offset, count);
        }

        /// <summary>Returns the text representation of the current instance.</summary>
        /// <returns>A <see cref="T:System.String"/> representing the current instance.</returns>
        public override String ToString()
        {
            return GetType().Name;
        }
        #endregion

        #region Methods (Abstract)
        /// <summary>Represents the core hashing function of the algorithm.</summary>
        /// <param name="buffer">The <see cref="T:System.Byte"/>[] whose hash must be computed.</param>
        /// <param name="offset">The offset into the byte array from which to begin using data.</param>
        /// <param name="count">The number of bytes in the array to use as data.</param>
        /// <returns>A <see cref="T:System.Byte"/>[] representing the computed hash.</returns>
        protected abstract Byte[] ComputeHashInternal(Byte[] buffer, Int32 offset, Int32 count);
        #endregion
    }
}