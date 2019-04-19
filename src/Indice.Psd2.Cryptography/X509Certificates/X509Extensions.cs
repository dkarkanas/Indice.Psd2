﻿using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Indice.Psd2.Cryptography;
using Indice.Psd2.Cryptography.X509Certificates;
using PemUtils;

namespace System.Security.Cryptography.X509Certificates
{
    /// <summary>
    /// Extension methods and utilities related to certificate generation and validation
    /// </summary>
    public static class CertificatesExtensions
    {
        /// <summary>
        /// Export a certificate to a PEM format string
        /// </summary>
        /// <param name="cert">The certificate to export</param>
        /// <returns>A PEM encoded string</returns>
        public static string ExportToPEM(this X509Certificate2 cert) {
            return MakePem(cert.Export(X509ContentType.Cert), "CERTIFICATE", insertLineBreaks: true);
        }

        private static string MakePem(byte[] ber, string header, bool insertLineBreaks = false) {
            StringBuilder builder = new StringBuilder("-----BEGIN ");
            builder.Append(header);
            builder.AppendLine("-----");

            string base64 = Convert.ToBase64String(ber, insertLineBreaks ? Base64FormattingOptions.InsertLineBreaks : Base64FormattingOptions.None);
            int offset = 0;
            const int LineLength = 64;

            while (offset < base64.Length) {
                int lineEnd = Math.Min(offset + LineLength, base64.Length);
                builder.AppendLine(base64.Substring(offset, lineEnd - offset));
                offset = lineEnd;
            }

            builder.Append("-----END ");
            builder.Append(header);
            builder.AppendLine("-----");
            return builder.ToString();
        }


        /// <summary>
        /// Export the public key to a PEM format string
        /// </summary>
        /// <param name="rsa">the RSA key</param>
        /// <returns>A PEM encoded string</returns>
        public static string ToSubjectPublicKeyInfo(this RSA rsa) {
            var pem = default(string);
            using (var stream = new MemoryStream()) {
                using (var writer = new PemWriter(stream))
                    writer.WritePublicKey(rsa);

                stream.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                    pem = reader.ReadToEnd();
                }
            }
            return pem;
        }

        /// <summary>
        /// Export the private  key to a PEM format string
        /// </summary>
        /// <param name="rsa">the RSA key</param>
        /// <returns>A PEM encoded string</returns>
        public static string ToRSAPrivateKey(this RSA rsa) {
            var pem = default(string);
            using (var stream = new MemoryStream()) {
                using (var writer = new PemWriter(stream))
                    writer.WritePrivateKey(rsa);

                stream.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                    pem = reader.ReadToEnd();
                }
            }
            return pem;
        }
        
        /// <summary>
        /// Export the private  key to a PEM format string
        /// </summary>
        /// <param name="pem">the RSA key</param>
        /// <returns>the RSA key</returns>
        public static RSAParameters ReadAsRSAKey(this string pem) {
            var key = default(RSAParameters);
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(pem)))
            using (var reader = new PemReader(stream)) {
                key = reader.ReadRsaKey();
            }
            return key;
        }

        /// <summary>
        /// Find the issuer certificate. 
        /// First checks if the leaf certificate's Subject and Issuer fields are not the same. 
        /// Otherwise, the certificate is the issuer (self-signed certificate)
        /// If different it instatniates a X509Chain object and passes leaf certificate to X509Chain.Build method.Examine ChainElements property (a collection) and element at index 1 is the issuer.
        /// </summary>
        /// <param name="leafCert"></param>
        /// <returns></returns>
        public static X509Certificate2 GetIssuer(X509Certificate2 leafCert) {
            if (leafCert.Subject == leafCert.Issuer) { return leafCert; }
            var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.Build(leafCert);
            X509Certificate2 issuer = null;
            if (chain.ChainElements.Count > 1) {
                issuer = chain.ChainElements[1].Certificate;
            }
            chain.Reset();
            return issuer;
        }

        /// <summary>
        /// Find the Psd2 QualifiedExtensions extension and extract the psd2 attributes.
        /// </summary>
        /// <param name="cert"></param>
        /// <returns></returns>
        public static Psd2Attributes GetPsd2Attributes(this X509Certificate2 cert) {
            var type = default(Psd2Attributes);
            var extension = cert.Extensions[QualifiedCertificateStatementsExtension.Oid_QC_Statements];
            if (extension != null) {
                var qcStatements = new QualifiedCertificateStatementsExtension(extension, extension.Critical);
                type = qcStatements.Psd2Type;
            }
            return type;
        }

        /// <summary>
        /// Find the SubjectKeyIdentifier extension and extract the key id.
        /// </summary>
        /// <param name="cert"></param>
        /// <returns></returns>
        public static string GetSubjectKeyIdentifier(this X509Certificate2 cert) {
            string keyid = null;
            var extension = cert.Extensions[AuthorityKeyIdentifierExtension.Oid_SubjectKeyIdentifier] as X509SubjectKeyIdentifierExtension;
            if (extension != null) {
                keyid = extension.SubjectKeyIdentifier;
            }
            return keyid;
        }
        
        /// <summary>
        /// Find the KeyIdentifier of the issuer certificate.
        /// </summary>
        /// <param name="cert"></param>
        /// <returns></returns>
        public static string GetAuthorityKeyIdentifier(this X509Certificate2 cert) {
            string keyid = null;
            var extension = cert.Extensions[AuthorityKeyIdentifierExtension.Oid_AuthorityKeyIdentifier];
            if (extension != null) {
                keyid = new AuthorityKeyIdentifierExtension(extension, extension.Critical).AuthorityKeyIdentifier;
            }
            return keyid;
        }

        /// <summary>
        /// Find the KeyIdentifier of the issuer certificate.
        /// </summary>
        /// <param name="cert"></param>
        /// <returns></returns>
        public static bool IsCertificateAuthority(this X509Certificate2 cert) {
            var extension = cert.Extensions["2.5.29.19"] as X509BasicConstraintsExtension;
            return extension?.CertificateAuthority == true;
        }
    }
}
