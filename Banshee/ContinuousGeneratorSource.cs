/*
 * Mirage - High Performance Music Similarity and Automatic Playlist Generator
 * http://hop.at/mirage
 * 
 * Copyright (C) 2007 Dominik Schnitzer <dominik@schnitzer.at>
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor,
 * Boston, MA  02110-1301, USA.
 */


using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

using Hyena;
using Banshee;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.ServiceStack;
using Banshee.Gui;
using Mirage;
using Mono.Unix;
using Gtk;
using Banshee.MediaEngine;
using System.Data.Sql;

namespace Banshee.Plugins.Mirage
{
    public class ContinuousGeneratorSource : PlaylistGeneratorSource
    {
        DatabaseTrackInfo processed;
        protected List<DatabaseTrackInfo> skipped = new List<DatabaseTrackInfo>();
    
        public ContinuousGeneratorSource(string name, Db db)
                : base(name, db)
        {
            processed = null;
            
            Properties.SetString ("Icon.Name", "source-mirage");
            
            InterfaceActionService action_service = ServiceManager.Get<InterfaceActionService> ("InterfaceActionService");
            action_service.PlaybackActions["NextAction"].Activated += OnNextPrevAction;
            action_service.PlaybackActions["PreviousAction"].Activated += OnNextPrevAction;
            
            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent, 
                                                      PlayerEvent.StartOfStream | 
                                                      PlayerEvent.Iterate);
        }

        public void OnNextPrevAction(object o, EventArgs e)
        {
            skipped.Add(ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo);
        }
        
        protected void NewPlaylist(int[] playlist, int length)
        {
            Gtk.Application.Invoke(delegate {
                List<DatabaseTrackInfo> rm = new List<DatabaseTrackInfo>();

                lock (track_model) {
                    // remove tracks, except seeds
                    foreach (DatabaseTrackInfo t in track_model) {
                        bool keepTrack = false;
                        for (int j = 0; j < seeds.Count; j++) {
                            if (seeds[j] == t) {
                                keepTrack = true;
                                break;
                            }
                        }
                        
                        if (!keepTrack) {
                            rm.Add(t);
                        }
                    }
                    
                    foreach (DatabaseTrackInfo t in rm) {
                        track_model.Remove(t);
                    }

                    int sameArtistCount = 0;
                    int i = 0;
                    int pi = 0;
                    while ((i < Math.Min(playlist.Length, length)) && (pi < playlist.Length)) {
                        DatabaseTrackInfo track = DatabaseTrackInfo.Provider.FetchSingle(playlist[pi]);
                        bool sameArtist = (String.Compare(track.Artist.Name, seeds[seeds.Count-1].Artist.Name, true) == 0);
                        pi++;
                        
                        if (sameArtist)
                            sameArtistCount++;
                        
                        if (sameArtist && (sameArtistCount > 0.5*length)) {
                            continue;
                        } else
                            i++;
                        
                        // FIXME : If this is not set, it causes crashes
                        track.CacheEntryId = 0;
                        track_model.Add(track);
                    }
                    SetStatus(Catalog.GetString("Playlist ready."), false);
                    // We need to reset the current track because its index has changed 
                    CurrentTrack = ServiceManager.PlayerEngine.CurrentTrack;
                    track_model.Reload ();
                }
                
                OnUpdated();
                HideStatus();
            });
        }
        
        private void SimilarTracks(List<DatabaseTrackInfo> tracks,
                List<DatabaseTrackInfo> exclude)
        {
            int[] trackId = new int[tracks.Count];
            for (int i = 0; i < tracks.Count; i++) {
                trackId[i] = tracks[i].TrackId;
            }

            int[] excludeTrackId = new int[exclude.Count];
            for (int i = 0; i < exclude.Count; i++) {
                excludeTrackId[i] = exclude[i].TrackId;
            }

            SimilarityCalculator sc =
                    new SimilarityCalculator(trackId, excludeTrackId,
                            db, NewPlaylist, 5);
            Thread similarityCalculatorThread =
                    new Thread(new ThreadStart(sc.Compute));
            similarityCalculatorThread.Start();
        }
        
        private void OnPlayerEvent(PlayerEventArgs args)
        {
            if (ServiceManager.SourceManager.ActiveSource != this) {
                return;
            }
            
            switch (args.Event) {
                case PlayerEvent.StartOfStream:
                    if (CurrentTrack != ServiceManager.PlayerEngine.CurrentTrack) {
                        CurrentTrack = ServiceManager.PlayerEngine.CurrentTrack;
                    }
                    if (NextTrack == null) {
                        // We're at the last track in the playlist, we need new tracks
                        Refresh ();
                    }
                    break;
                case PlayerEvent.Iterate:
                    // if more than 60% of a track is played, use this track as
                    // a seed song for the next tracks.
                    if ((processed != ServiceManager.PlayerEngine.CurrentTrack) &&
                            (ServiceManager.PlayerEngine.Position > 
                             ServiceManager.PlayerEngine.Length * 0.6)) {
                        Refresh ();
                    }
                    break;
            }
        }

        private void Refresh ()
        {
            processed = ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo;
            lock (track_model) {   
                if (!seeds.Contains(processed)) {
                    if (!track_model.Contains(processed)) {
                        // We're playing another source
                        return;
                    }
                    List<DatabaseTrackInfo> t = new List<DatabaseTrackInfo>();
                    t.Add(processed);
                    seeds.Add(processed);
                    
                    List<DatabaseTrackInfo> skip = new List<DatabaseTrackInfo>();
                    
                    skip.AddRange(seeds);
                    if (skipped.Count > 0)
                        skip.AddRange(skipped);
                    SimilarTracks(t, skip);
                }
            }
        }
    }
}
