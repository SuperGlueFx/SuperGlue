﻿using System.Collections.Generic;
using System.Linq;
using Raven.Client.Linq;
using SuperGlue.Configuration;

namespace SuperGlue.RavenDb.Search
{
    public class DefaultRavenSearch : IRavenSearch
    {
        private readonly IParseSearchString _parseSearchString;
        private readonly IDictionary<string, object> _environment;

        public DefaultRavenSearch(IParseSearchString parseSearchString, IDictionary<string, object> environment)
        {
            _parseSearchString = parseSearchString;
            _environment = environment;
        }

        public IRavenQueryable<TEntity> Search<TEntity>(IRavenQueryable<TEntity> query, string search, params string[] specialCommands)
        {
            var parsingResult = _parseSearchString.Parse(search, specialCommands);

            var searchParts = _environment.ResolveAll<IRavenSearchPart<TEntity>>().ToList();
            var specialsParts = _environment.ResolveAll<IRavenSpecialCommandSearch<TEntity>>().ToList();
            var handleLeftOvers = _environment.ResolveAll<IHandleLeftoverSearchPart<TEntity>>().ToList();
            var freeTextSearcher = _environment.Resolve<IRavenFreeTextSearch<TEntity>>();

            var leftoverParts = new List<SearchPart>();

            foreach (var part in parsingResult.Parts)
            {
                var currentPart = part;
                var searchers = searchParts.Where(x => x.Part == currentPart.Name).ToList();

                if (!searchers.Any())
                {
                    leftoverParts.Add(part);
                    continue;
                }

                query = searchers.Aggregate(query, (current, searcher) => searcher.ApplyTo(current, currentPart.Value));
            }

            query = leftoverParts
                            .Aggregate(query, (current1, currentPart) => handleLeftOvers
                                                                                    .Aggregate(current1, (current, searcher) => searcher.ApplyTo(current, currentPart.Name, currentPart.Value)));

            query = parsingResult
                .SpecialCommands
                .Select(currentPart => specialsParts
                                            .Where(x => x.Command == currentPart)
                                            .ToList())
                .Aggregate(query, (current1, searchers) => searchers
                                                                .Aggregate(current1, (current, searcher) => searcher.ApplyTo(current)));

            return freeTextSearcher == null ? query : freeTextSearcher.ApplyTo(query, parsingResult.FreeSearch);
        }
    }
}