﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SuperGlue.Web.Http
{
    public class CacheOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CacheOptions"/> class.
        /// </summary>
        /// <param name="absoluteExpiry">The absolute expiry time.</param>
        /// <remarks>Use this constructor when you want to specify absolute expiry.</remarks>
        public CacheOptions(DateTime absoluteExpiry)
        {
            AbsoluteExpiry = absoluteExpiry;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheOptions"/> class.
        /// </summary>
        /// <param name="slidingExpiry">The sliding expiry time.</param>
        /// <remarks>Use this constructor when you want to specify sliding expiry.</remarks>
        public CacheOptions(TimeSpan slidingExpiry)
        {
            SlidingExpiry = slidingExpiry;
        }

        private CacheOptions() { }

        /// <summary>
        /// Gets the absolute expiry time.
        /// </summary>
        public DateTime? AbsoluteExpiry { get; }

        /// <summary>
        /// Gets a value indicating whether caching should be disabled for a resource.
        /// </summary>
        /// <value>
        ///   <c>true</c> if caching should be disabled; otherwise, <c>false</c>.
        /// </value>
        public bool Disable => !(SlidingExpiry.HasValue || AbsoluteExpiry.HasValue);

        /// <summary>
        /// Gets or sets a value indicating the level at which the response may be cached.
        /// </summary>
        /// <value>A <see cref="CacheLevel"/> value.</value>
        public CacheLevel Level { get; set; }

        /// <summary>
        /// Gets the sliding expiry time.
        /// </summary>
        public TimeSpan? SlidingExpiry { get; }

        /// <summary>
        /// Gets or sets names of headers which should be considered by the caching systems.
        /// </summary>
        /// <value>
        /// The headers to vary by.
        /// </value>
        public ICollection<string> VaryByHeaders { get; set; }

        /// <summary>
        /// Formats the options as a Cache-Control header value.
        /// </summary>
        /// <returns>The Cache-Control header string.</returns>
        public string ToHeaderString()
        {
            var builder = new StringBuilder(CacheLevelToString(Level));
            if (SlidingExpiry.HasValue)
            {
                builder.AppendFormat(", max-age={0}", SlidingExpiry.Value.TotalSeconds);
            }
            return builder.ToString();
        }

        /// <summary>
        /// Use this single instance to disable caching.
        /// </summary>
        public static readonly CacheOptions DisableCaching = new CacheOptions();

        private static string CacheLevelToString(CacheLevel cacheLevel)
        {
            switch (cacheLevel)
            {
                case CacheLevel.Public:
                    return "public";
                case CacheLevel.Private:
                    return "private";
            }
            return "no-cache";
        }
    }
}