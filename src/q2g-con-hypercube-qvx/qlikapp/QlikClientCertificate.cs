#region License
/*
Copyright (c) 2017 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace q2gconhypercubeqvx.QlikApplication
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Cryptography;
    using System.IO;
    #endregion

    public class QlikClientCertificate
    {
        public enum PemStringType
        {
            Certificate,
            RsaPrivateKey
        }

        #region Properties & Variables
        private string PublicCertificate { get; set; }
        private string PrivateKey { get; set; }
        private string Password { get; set; }
        private bool IsSingleFile { get; set; }
        #endregion

        #region Constructor
        public QlikClientCertificate(string certKeyFilePath, string password)
        {
            PublicCertificate = File.ReadAllText(certKeyFilePath);
            IsSingleFile = true;
            Password = password;
        }

        public QlikClientCertificate(string certPath, string keyPath, string password)
        {
            if (!File.Exists(certPath))
                throw new Exception($"The client certificate {certPath} was not found.");

            if (!File.Exists(keyPath))
                throw new Exception($"The client key {keyPath} was not found.");

            PublicCertificate = File.ReadAllText(certPath);
            PrivateKey = File.ReadAllText(keyPath);
            IsSingleFile = false;
            Password = password;
        }
        #endregion

        #region Static Helper Functions
        //This function parses an integer size from the reader using the ASN.1 format
        private static int DecodeIntegerSize(System.IO.BinaryReader rd)
        {
            var count = -1;

            var byteValue = rd.ReadByte();
            if (byteValue != 0x02)
                return 0;

            byteValue = rd.ReadByte();
            if (byteValue == 0x81)
                count = rd.ReadByte();
            else if (byteValue == 0x82)
            {
                var hi = rd.ReadByte();
                var lo = rd.ReadByte();
                count = BitConverter.ToUInt16(new[] { lo, hi }, 0);
            }
            else
                count = byteValue;        // we already have the data size

            //remove high order zeros in data
            while (rd.ReadByte() == 0x00)
                count -= 1;

            rd.BaseStream.Seek(-1, SeekOrigin.Current);
            return count;
        }

        private static byte[] GetBytesFromPEM(string pemString, PemStringType type)
        {
            string header;
            string footer;

            switch (type)
            {
                case PemStringType.Certificate:
                    header = "-----BEGIN CERTIFICATE-----";
                    footer = "-----END CERTIFICATE-----";
                    break;
                case PemStringType.RsaPrivateKey:
                    header = "-----BEGIN RSA PRIVATE KEY-----";
                    footer = "-----END RSA PRIVATE KEY-----";
                    break;
                default:
                    return null;
            }

            var start = pemString.IndexOf(header) + header.Length;
            var end = pemString.IndexOf(footer, start) - start;
            return Convert.FromBase64String(pemString.Substring(start, end));
        }

        private static byte[] AlignBytes(byte[] inputBytes, int alignSize)
        {
            var inputBytesSize = inputBytes.Length;
            if ((alignSize != -1) && (inputBytesSize < alignSize))
            {
                var buf = new byte[alignSize];
                for (int i = 0; i < inputBytesSize; ++i)
                    buf[i + (alignSize - inputBytesSize)] = inputBytes[i];

                return buf;
            }
            else
            {
                //Already aligned, or doesn't need alignment
                return inputBytes;   
            }
        }

        //This helper function parses an RSA private key using the ASN.1 format
        private static RSACryptoServiceProvider DecodeRsaPrivateKey(byte[] privateKeyBytes)
        {
            var ms = new MemoryStream(privateKeyBytes);
            var rd = new BinaryReader(ms);

            try
            {
                var shortValue = rd.ReadUInt16();
                switch (shortValue)
                {
                    case 0x8130:
                        // If true, data is little endian since the proper logical seq is 0x30 0x81
                        rd.ReadByte(); //advance 1 byte
                        break;
                    case 0x8230:
                        rd.ReadInt16();  //advance 2 bytes
                        break;
                    default:
                        return null;
                }

                shortValue = rd.ReadUInt16();
                if (shortValue != 0x0102) // (version number)
                    return null;

                var byteValue = rd.ReadByte();
                if (byteValue != 0x00)
                    return null;
                
                // The data following the version will be the ASN.1 data itself, which in our case
                // are a sequence of integers.

                // In order to solve a problem with instancing RSACryptoServiceProvider
                // via default constructor on .net 4.0 this is a hack
                var parms = new CspParameters();
                parms.Flags = CspProviderFlags.NoFlags;
                parms.KeyContainerName = Guid.NewGuid().ToString().ToUpperInvariant();
                parms.ProviderType = ((Environment.OSVersion.Version.Major > 5) || ((Environment.OSVersion.Version.Major == 5) && (Environment.OSVersion.Version.Minor >= 1))) ? 0x18 : 1;

                var rsa = new RSACryptoServiceProvider(parms);
                var rsAparams = new RSAParameters();
                rsAparams.Modulus = rd.ReadBytes(DecodeIntegerSize(rd));

                // Argh, this is a pain.  From emperical testing it appears to be that RSAParameters doesn't like byte buffers that
                // have their leading zeros removed.  The RFC doesn't address this area that I can see, so it's hard to say that this
                // is a bug, but it sure would be helpful if it allowed that. So, there's some extra code here that knows what the
                // sizes of the various components are supposed to be.  Using these sizes we can ensure the buffer sizes are exactly
                // what the RSAParameters expect.  Thanks, Microsoft.
                var traits = new RSAParameterTraits(rsAparams.Modulus.Length * 8);

                rsAparams.Modulus = AlignBytes(rsAparams.Modulus, traits.size_Mod);
                rsAparams.Exponent = AlignBytes(rd.ReadBytes(DecodeIntegerSize(rd)), traits.size_Exp);
                rsAparams.D = AlignBytes(rd.ReadBytes(DecodeIntegerSize(rd)), traits.size_D);
                rsAparams.P = AlignBytes(rd.ReadBytes(DecodeIntegerSize(rd)), traits.size_P);
                rsAparams.Q = AlignBytes(rd.ReadBytes(DecodeIntegerSize(rd)), traits.size_Q);
                rsAparams.DP = AlignBytes(rd.ReadBytes(DecodeIntegerSize(rd)), traits.size_DP);
                rsAparams.DQ = AlignBytes(rd.ReadBytes(DecodeIntegerSize(rd)), traits.size_DQ);
                rsAparams.InverseQ = AlignBytes(rd.ReadBytes(DecodeIntegerSize(rd)), traits.size_InvQ);

                rsa.ImportParameters(rsAparams);
                return rsa;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                rd.Close();
            }
        }
        #endregion

        #region public methods
        public X509Certificate2 GetCertificateFromPEM(string friendlyName = "QlikClient")
        {
            try
            {
                var certBuffer = GetBytesFromPEM(PublicCertificate, PemStringType.Certificate);
                var keyBuffer = new byte[0];
                if (IsSingleFile)
                    keyBuffer = GetBytesFromPEM(PublicCertificate, PemStringType.RsaPrivateKey);
                else
                    keyBuffer = GetBytesFromPEM(PrivateKey, PemStringType.RsaPrivateKey);

                var newCertificate = new X509Certificate2(certBuffer, Password);
                newCertificate.PrivateKey = DecodeRsaPrivateKey(keyBuffer);
                newCertificate.FriendlyName = friendlyName;
                return newCertificate;
            }
            catch (Exception ex)
            {
                throw new Exception("The certificate could not be created.", ex);
            }
        }
        #endregion
    }

    internal class RSAParameterTraits
    {
        #region Fields
        public int size_Mod = -1;
        public int size_Exp = -1;
        public int size_D = -1;
        public int size_P = -1;
        public int size_Q = -1;
        public int size_DP = -1;
        public int size_DQ = -1;
        public int size_InvQ = -1;
        #endregion

        #region public methods
        public RSAParameterTraits(int modulusLengthInBits)
        {
            // The modulus length is supposed to be one of the common lengths, which is the commonly referred to strength of the key,
            // like 1024 bit, 2048 bit, etc.  It might be a few bits off though, since if the modulus has leading zeros it could show
            // up as 1016 bits or something like that.
            var assumedLength = -1;
            var logbase = Math.Log(modulusLengthInBits, 2);
            if (logbase == (int)logbase)
            {
                // It's already an even power of 2
                assumedLength = modulusLengthInBits;
            }
            else
            {
                // It's not an even power of 2, so round it up to the nearest power of 2.
                assumedLength = (int)(logbase + 1.0);
                assumedLength = (int)(Math.Pow(2, assumedLength));
            }

            switch (assumedLength)
            {
                case 1024:
                    size_Mod = 0x80;
                    size_Exp = -1;
                    size_D = 0x80;
                    size_P = 0x40;
                    size_Q = 0x40;
                    size_DP = 0x40;
                    size_DQ = 0x40;
                    size_InvQ = 0x40;
                    break;
                case 2048:
                    size_Mod = 0x100;
                    size_Exp = -1;
                    size_D = 0x100;
                    size_P = 0x80;
                    size_Q = 0x80;
                    size_DP = 0x80;
                    size_DQ = 0x80;
                    size_InvQ = 0x80;
                    break;
                case 4096:
                    size_Mod = 0x200;
                    size_Exp = -1;
                    size_D = 0x200;
                    size_P = 0x100;
                    size_Q = 0x100;
                    size_DP = 0x100;
                    size_DQ = 0x100;
                    size_InvQ = 0x100;
                    break;
                default:
                    break;
            }
        }
        #endregion
    }
}
