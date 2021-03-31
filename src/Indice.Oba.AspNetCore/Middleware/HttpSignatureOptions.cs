﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Indice.Oba.AspNetCore.Middleware
{
    /// <summary>
    /// Provides programmatic configuration for the <see cref="HttpSignatureMiddleware"/>.
    /// </summary>
    public class HttpSignatureOptions
    {
        /// <summary>
        /// Paths that are exluded from <see cref="Mappings"/>, optionally based on provided HTTP method.
        /// </summary>
        public Dictionary<string, string> IgnoredPaths { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        /// <summary>
        /// Map of route paths and header names that will be included in the signatures.
        /// </summary>
        public Dictionary<string, List<string>> Mappings { get; } = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
        /// <summary>
        /// The header name where the certificate used for signing the request will reside, in base64 encoding. This header will be present in the request object if a signature is contained.
        /// </summary>
        public string RequestSignatureCertificateHeaderName { get; } = "TTP-Signature-Certificate";
        /// <summary>
        /// The header name where the certificate used for validating the response will reside, in base64 encoding. This header will be present in the request object if a signature is contained.
        /// </summary>
        public string ResponseSignatureCertificateHeaderName { get; } = "ASPSP-Signature-Certificate";
        /// <summary>
        /// The header name where the Response Id will be populated.  This is usualy a GUID.
        /// </summary>
        public string ResponseIdHeaderName { get; } = "X-Response-Id";
        /// <summary>
        /// The header name where the request was created. Defaults to 'X-Date'. This header is used when validating the signature of a request as the (created) parameter.
        /// </summary>
        public string RequestCreatedHeaderName { get; } = "X-Date";
        /// <summary>
        /// The header name where the response was created. Defaults to 'X-Date'. This header is used when generating the signature for the response as the (created) parameter.
        /// </summary>
        public string ResponseCreatedHeaderName { get; } = "X-Date";
        /// <summary>
        /// Enables request validation.
        /// </summary>
        public bool RequestValidation { get; set; } = true;
        /// <summary>
        /// Enalbes response signing.
        /// </summary>
        public bool? ResponseSigning { get; set; } = true;

        /// <summary>
        /// Adds a new map entry to the dictionary of mappings. This will be picked up by the <see cref="HttpSignatureMiddleware"/> in order to determine which headers are included in each transmission.
        /// </summary>
        /// <param name="path">The path to map.</param>
        /// <param name="headerNames">The headers to be included in the signature for this path.</param>
        public HttpSignatureOptions MapPath(PathString path, params string[] headerNames) {
            if (path.HasValue && path.Value.EndsWith("/", StringComparison.Ordinal)) {
                throw new ArgumentException("The path must not end with a '/'", nameof(path));
            }
            if (path.HasValue) {
                Mappings.Add(path.Value, new List<string>(headerNames));
            }
            return this;
        }

        /// <summary>
        /// Excludes a mapped path, optionally based on the given HTTP method. If HTTP method is not specified, every request to this path will not be used by <see cref="HttpSignatureMiddleware"/>.
        /// </summary>
        /// <param name="path">The path to exclude.</param>
        /// <param name="httpMethod">The HTTP methods to exclude for the given path.</param>
        public HttpSignatureOptions IgnorePath(PathString path, string httpMethod = null) {
            // No HTTP methods specified, so exclude just the path (implies that all HTTP methods will be excluded for this path).
            if (string.IsNullOrWhiteSpace(httpMethod)) {
                IgnoredPaths.Add(path.Value, "*");
                return this;
            }
            // Validate HTTP method.
            // There are more of course, but this seems enough for our needs.
            var isValidHttpMethod = HttpMethods.IsGet(httpMethod) || HttpMethods.IsPost(httpMethod) || HttpMethods.IsPut(httpMethod) || HttpMethods.IsDelete(httpMethod) || HttpMethods.IsPatch(httpMethod);
            if (!isValidHttpMethod) {
                throw new ArgumentException($"HTTP method {httpMethod} is not valid.");
            }
            IgnoredPaths.Add(path.Value, httpMethod);
            return this;
        }

        /// <summary>
        /// Tries to find a matching path.
        /// </summary>
        /// <param name="path">The path to match.</param>
        /// <param name="httpMethod">The HTTP method of the specified path.</param>
        /// <param name="headerNames">The headers to be included in the signature for this path.</param>
        public bool TryMatch(PathString path, string httpMethod, out List<string> headerNames) {
            headerNames = null;
            if (Mappings.ContainsKey(path)) {
                headerNames = Mappings[path];
                return !IsIgnoredPath(path, httpMethod);
            }
            var results = Mappings.Where(x => path.StartsWithSegments(x.Key));
            if (results.Any()) {
                headerNames = results.OrderByDescending(x => x.Key.Length).First().Value;
                return !IsIgnoredPath(path, httpMethod);
            }
            return false;
        }

        /// <summary>
        /// Tries to find a matching path.
        /// </summary>
        /// <param name="httpContext">The path to match.</param>
        /// <param name="headerNames">The headers to be included in the signature for this path.</param>
        public bool TryMatch(HttpContext httpContext, out List<string> headerNames) {
            var path = httpContext.Request.Path;
            var httpMethod = httpContext.Request.Method;
            var isMatch = TryMatch(path, httpMethod, out var headerNamesInner);
            headerNames = headerNamesInner;
            return isMatch;
        }

        private bool IsIgnoredPath(PathString path, string httpMethod) {
            var isIgnoredpath = IgnoredPaths.ContainsKey(path) && (string.IsNullOrWhiteSpace(httpMethod) || IgnoredPaths[path].Equals(httpMethod, StringComparison.OrdinalIgnoreCase));
            if (isIgnoredpath) {
                return true;
            }
            var paths = IgnoredPaths.Where(x => path.StartsWithSegments(x.Key));
            if (!paths.Any()) {
                return false;
            }
            var basePath = paths.OrderBy(x => x.Key.Length).First().Key;
            var isIgnoredSubPath = IgnoredPaths.ContainsKey(basePath) && (IgnoredPaths[basePath].Equals(httpMethod, StringComparison.OrdinalIgnoreCase) || IgnoredPaths[basePath].Equals("*"));
            if (isIgnoredSubPath) {
                return true;
            }
            return false;
        }
    }
}
