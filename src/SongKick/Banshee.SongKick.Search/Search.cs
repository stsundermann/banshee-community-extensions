//
// Search.cs
//
// Author:
//   Tomasz Maczyński <tmtimon@gmail.com>
//
// Copyright 2013 Tomasz Maczyński
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using Banshee.SongKick.Recommendations;
using Banshee.SongKick.Network;
using Hyena.Jobs;

namespace Banshee.SongKick.Search
{
    public abstract class Search<T> where T : Result
    {
        List<ColumnHeader> result_fields = new List<ColumnHeader> ();

        public IList<ColumnHeader>  ReturnFields { get { return result_fields; } }
        public String Query { get; protected set; }
        public SearchType SearchType { get; protected set; }
        public ResultsPage<T> ResultsPage { get; protected set; }

        protected SongKickDownloader downloader;

        public Search (SearchType searchType, string query)
        {
            Query = query;
            SearchType = searchType;
            downloader = new SongKickDownloader (SongKickCore.APIKey);
            result_fields.AddRange (ColumnHeader.ColumnHeaders);
        }

        public abstract void GetResultsPage ();

        public override string ToString ()
        {
            return string.Format ("[Search: Query={0}, SearchType={1}, ReturnFields={2}]", Query, SearchType.Id, ReturnFields);
        }
    }

    public class MetroAreaByIdSearch : Search<Event> {

        public MetroAreaByIdSearch(SearchType searchType, string query) 
            : base(searchType, query)
        {
        }

        public override void GetResultsPage ()
        {
            ResultsPage = downloader.getMetroareaMusicEvents (Query, Banshee.SongKick.Recommendations.Events.GetMusicEventListResultsDelegate);
        }
    }
}

