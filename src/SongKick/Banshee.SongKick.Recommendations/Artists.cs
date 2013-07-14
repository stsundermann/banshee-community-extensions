//
// Artists.cs
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
using Hyena.Json;

namespace Banshee.SongKick.Recommendations
{
    public class Artists : Results
    {
        public IList<Event> elements { get; set; }

        public Artists (JsonObject jsonObject)
        {
            this.elements = new List<Event> ();
            var eventJsonObjs = jsonObject["artist"] as JsonArray;

            foreach (var eventJsonObj in eventJsonObjs) 
            {
                elements.Add (new Event(eventJsonObj as JsonObject));
            }
        }

        public static ResultsPage.GetResultsDelegate GetArtistListResultsDelegate = 
            new ResultsPage.GetResultsDelegate(GetArtists);

        public static Artists GetArtists(JsonObject jsonObject)
        {
            return new Artists (jsonObject as JsonObject);
        }
    }
}
