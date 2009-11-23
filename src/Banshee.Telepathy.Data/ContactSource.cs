//
// ContactSource.cs
//
// Author:
//   Neil Loknath <neil.loknath@gmail.com>
//
// Copyright (C) 2009 Neil Loknath
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using Mono.Unix;

using Hyena;
using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Gui.Dialogs;
using Banshee.Library;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.Telepathy.Gui;
using Banshee.Telepathy.DBus;
using Banshee.Telepathy.Net;

using Banshee.Telepathy.API;
using Banshee.Telepathy.API.Data;
using Banshee.Telepathy.API.Dispatchables;

namespace Banshee.Telepathy.Data
{
    public enum ContactSourceState {
        Unloaded,
        Waiting,
        PermissionGranted,
        PermissionNotGranted,
        LoadingMetadata,
        LoadedMetadata,
        LoadingPlaylists,
        Loaded
    };
    
    public class ContactSource : PrimarySource, IContactSource
    {
        private const int chunk_length = 250;
        private readonly DownloadMonitor download_monitor = new DownloadMonitor ();
        private ContactRequestDialog dialog;
        
        private delegate bool GetBoolPropertyCaller ();
        private GetBoolPropertyCaller permission_caller;
        private GetBoolPropertyCaller downloading_caller;
        
        private DBusActivity current_activity = null;
        
        private static readonly string tmp_download_path = Paths.Combine (TelepathyService.CacheDirectory, "partial-downloads");
        public static string TempDownloadDirectory {
            get { return tmp_download_path; }
        }
        
        private HyenaSqliteCommand purge_source_command = new HyenaSqliteCommand (@"
            DELETE FROM CorePrimarySources WHERE PrimarySourceId = ?
        ");
                
        public ContactSource (Contact contact) : base (Catalog.GetString ("Contact"), 
                                                       String.Format ("{0} ({1})", 
                                                                      contact != null ? contact.Name : String.Empty, 
                                                                      contact != null ? contact.Status.ToString () : String.Empty),
                                                       contact !=null ? contact.ToString () : String.Empty, 
                                                       300)
        {
            Contact = contact;
            Contact.ContactUpdated += OnContactUpdated;
            Log.DebugFormat ("ContactSource created for {0}", Contact.Name);
            
            //Properties.SetString ("UnmapSourceActionLabel", Catalog.GetString ("Disconnect"));
            //Properties.SetString ("UnmapSourceActionIconName", "gtk-disconnect");
            Properties.SetString ("Icon.Name", "stock_person");
            Properties.SetString ("ActiveSourceUIResource", "ActiveSourceUI.xml");
            Properties.Set<bool> ("ActiveSourceUIResourcePropagate", true);
            Properties.SetString ("GtkActionPath", "/ContactSourcePopup");

            Properties.SetString ("TrackView.ColumnControllerXml", @"
                    <column-controller>
                      <add-all-defaults />
                      <column modify-default=""IndicatorColumn"">
                          <renderer type=""Banshee.Telepathy.Gui.ColumnCellContactStatusIndicator"" />
                      </column>
                    </column-controller>
                "
            );
            
            SupportsPlaylists = false;
            
            if (SavedCount > 0) {
                CleanUpData ();
            }
            SavedCount = 0;

            ContactSourceInitialize ();
            AfterInitialized ();
        }

        private ContactSourceState state = ContactSourceState.Unloaded;
        public ContactSourceState State {
            get { return state; }
        }
            
        private Contact contact;
        public Contact Contact {
            get { return contact; }
            protected set {
                if (value == null) {
                   throw new ArgumentNullException ("contact");
                }
                contact = value;
            }
        }
        
        public string AccountId {
            get {
                if (Contact != null) {
                    return Contact.AccountId;
                }
                return String.Empty;
            }
        }

        public string ContactName {
            get { 
                if (Contact != null) {
                    return Contact.Name; 
                }
                return String.Empty;
            }
        }

        public string ContactStatus {
            get {
                if (Contact != null) {
                    return Contact.Status.ToString (); 
                }
                return String.Empty;
            }
        }
        
        public override bool CanRemoveTracks {
            get { return false; }
        }

        public override bool CanDeleteTracks {
            get { return false; }
        }

        public override bool ConfirmRemoveTracks {
            get { return false; }
        }

        public override bool HasEditableTrackProperties {
            get { return false; }
        }
        
        private bool can_activate = true;
        public override bool CanActivate {
            get { return can_activate; }
        }

        private bool is_temporary = true;
        public bool IsTemporary {
            get { return is_temporary; }
        }

        private bool downloading_allowed = true;
        public bool IsDownloadingAllowed {
            get { return downloading_allowed; }
        }
            
        public static void StopSharing (ContactSource source)
        {
            if (source == null) {
                return;
            }
            
            Contact contact = source.Contact;
            Activity activity = contact.DispatchManager.Get <DBusActivity> (contact, MetadataProviderService.BusName);
            if (activity != null) {
                activity.Close ();
            }
            
            StopStreaming (source);
        }
        
        public static void StopStreaming (ContactSource source)
        {
            if (source == null) {
                return;
            }
            
            Contact contact = source.Contact;
            Activity activity = contact.DispatchManager.Get <StreamActivityListener> (contact, StreamingServer.ServiceName);
            if (activity != null) {
                activity.Close ();
            }
        }
        
        protected override void Initialize ()
        { 
            base.Initialize ();
            ContactSourceInitialize ();
        }

        private void PurgeSelf ()
        {
            ServiceManager.DbConnection.Execute (purge_source_command, DbId);
        }

        public void CleanUpData ()
        {
            PurgeTracks ();

            List<Source> children = new List<Source> (Children);
            foreach (Source child in children) {
                if (child is Banshee.Sources.IUnmapableSource) {
                    (child as Banshee.Sources.IUnmapableSource).Unmap ();
                }
            }
            
            ClearChildSources ();
        }
            
        public override void Dispose ()
        {
            can_activate = false;
            
            DBusActivity.ResponseRequired -= OnActivityResponseRequired;
            DBusActivity.Ready -= OnActivityReady;
            DBusActivity.Closed -= OnActivityClosed;

            CleanUpData ();

//            if (Contact != null) {
//                DispatchManager dm  = Contact.DispatchManager;
//                dm.RemoveAll (contact);
//            }
            
            if (is_temporary) {
                PurgeSelf ();
            }
            
            base.Dispose ();
        }
        
        private void ContactSourceInitialize ()
        {
            // let's listen for requests from our contacts
            DBusActivity.ResponseRequired += OnActivityResponseRequired;
            DBusActivity.Ready += OnActivityReady;
            DBusActivity.Closed += OnActivityClosed;

            download_monitor.AllFinished += OnAllDownloadsFinished;
            download_monitor.AllProcessed += OnAllDownloadsProcessed;

            TrackExternalObjectHandler = GetContactTrackInfoObject;
        }

        public override void Activate ()
        {
            if (Contact == null) {
                Log.Error ("ContactSource.Activate found contact is null.");
                return;
            }
            
            Log.DebugFormat ("{0} selected", Contact.Name);

            EnsureDBusActivity ();
            
            if (current_activity != null) {
                // user clicked to browse a contact, but contact on the other end sent a 
                // request also. The tube is probably slow and states have not changed yet,
                // so set waiting
                if (current_activity.State == ActivityState.RemotePending) {
                    SetWaiting ();
                } else {
                    try {
                        LoadData (current_activity);
                    } catch (Exception e) {
                        Log.Exception (e);
                    }
                }
                
            } else {
                if (Contact.SupportedChannels.GetChannelInfo <DBusTubeChannelInfo> (MetadataProviderService.BusName) != null) {
                    if (state == ContactSourceState.Unloaded) {
                        SetWaiting ();
                        RequestDBusTube ();                    
                    }
                } else {
                    SetStatus (Catalog.GetString ("Contact does not support Telepathy extension"), true);
                }
            }

            base.Activate ();
        }

        private object GetContactTrackInfoObject (DatabaseTrackInfo track)
        {
            return new ContactTrackInfo (track, this);
        }

        private void EnsureDBusActivity ()
        {
            if (Contact == null) {
                return;
            }
            
            if (current_activity == null) {
                DispatchManager dm = Contact.DispatchManager;
                current_activity = dm.Get <DBusActivity> (contact, MetadataProviderService.BusName);
            }
        }
        
        private void RequestDBusTube ()
        {
            IDictionary <string, object> properties = new Dictionary <string, object> ();
            properties.Add ("ServiceName", MetadataProviderService.BusName);

            try {
                Contact.DispatchManager.Request <DBusActivity> (Contact, properties);
            } catch (Exception e) {
                Log.Exception (e);
            }
        }
        
        private void RequestStreamTube ()
        {
            try {
                if (!Contact.DispatchManager.Exists <StreamActivityListener> (Contact, StreamingServer.ServiceName)) {
                    IDictionary <string, object> properties = new Dictionary <string, object> ();
                    properties.Add ("Service", StreamingServer.ServiceName);
                
                    Contact.DispatchManager.Request <StreamActivityListener> (Contact, properties);
                }
            } catch (Exception e) {
                Log.Exception (e);
            }
        }
        
#region Activity Interaction
        
        private void RegisterActivityServices (DBusActivity activity)
        {
            RegisterActivityServices (activity, true);
        }

        private void RegisterActivityServices (DBusActivity activity, bool permission)
        {
            if (activity == null) {
                throw new ArgumentNullException ("activity");
            }
            
            IMetadataProviderService provider_service = new MetadataProviderService (activity, permission);
            activity.RegisterDBusObject (provider_service, MetadataProviderService.ObjectPath);
        }

        private void GetDownloadingAllowedCallback (IAsyncResult result)
        {
            GetBoolPropertyCaller caller = (GetBoolPropertyCaller) result.AsyncState;
            downloading_allowed = caller.EndInvoke (result);
        }
        
        private void GetPermissionCallback (IAsyncResult result)
        {
            if (state != ContactSourceState.Waiting) {
                return;
            }
            
            GetBoolPropertyCaller caller = (GetBoolPropertyCaller) result.AsyncState;
            bool granted = caller.EndInvoke (result);
            
            if (granted) {
                state = ContactSourceState.PermissionGranted;                
            } else {
                state = ContactSourceState.PermissionNotGranted;
            }
            
            LoadData (current_activity);
        }
        
//        private void LoadData ()
//        {
//            if (Contact == null) {
//                throw new InvalidOperationException ("Contact is null.");
//            }
//            
//            DBusActivity activity = Contact.DispatchManager.Get <DBusActivity> (contact, MetadataProviderService.BusName);
//            LoadData (activity);
//        }

        private void SetWaiting ()
        {
            state = ContactSourceState.Waiting;
            SetStatus (Catalog.GetString ("Waiting for response from contact"), false);
        }
        
        private void LoadData (DBusActivity activity)
        {
            if (state >= ContactSourceState.LoadingMetadata) {
                return;
            } else if (activity == null) {
                throw new ArgumentNullException ("activity");
            } else if (activity.State != ActivityState.Connected) {
                throw new InvalidOperationException (String.Format ("activity state {0} is invalid.", activity.State));
            }
        
            IMetadataProviderService service = activity.GetDBusObject <IMetadataProviderService> (MetadataProviderService.BusName, MetadataProviderService.ObjectPath);
            if (service == null) {
                throw new InvalidOperationException ("ContactSource.LoadData found service null");
            }

            try {
                // call MetadataProviderService.PermissionGranted () asynchronously to prevent blocking the UI
                // when Telepathy tubes are slow
                if (state <= ContactSourceState.Waiting) {
                    SetWaiting ();
                    permission_caller = new GetBoolPropertyCaller (service.PermissionGranted);
                    permission_caller.BeginInvoke (new AsyncCallback (GetPermissionCallback), permission_caller);
    
                } else if (state == ContactSourceState.PermissionGranted) {
                    service.DownloadingAllowedChanged += delegate (bool allowed) {
                        downloading_allowed = allowed;            
                    };
                
                    // determine if downloading is allowed asynchronously
                    downloading_caller = new GetBoolPropertyCaller  (service.DownloadsAllowed);
                    downloading_caller.BeginInvoke (new AsyncCallback (GetDownloadingAllowedCallback), downloading_caller);
                    
                    // clean up any residual tracks
                    download_monitor.Reset ();
                    SetStatus (Catalog.GetString ("Loading..."), false);
                    state = ContactSourceState.LoadingMetadata;
                    CleanUpData ();
    
                    string metadata_path = service.CreateMetadataProvider (LibraryType.Music).ToString ();
                    IMetadataProvider library_provider = activity.GetDBusObject <IMetadataProvider> (MetadataProvider.BusName, metadata_path);

                    Download download = new Download ();
                    download_monitor.Add (metadata_path, download);

                    download.ProcessIncomingPayloads (delegate (object sender, object [] o) {

                        IDictionary <string, object> [] chunk = o as IDictionary <string, object> [];
                        if (chunk == null) {
                            return;
                        }
                        
                        HyenaSqliteConnection conn = ServiceManager.DbConnection;
                        conn.BeginTransaction ();
                        
                        for (int i = 0; i < chunk.Length; i++) {
                            IDictionary <string, object> track = chunk[i];
                            ContactTrackInfo contact_track = new ContactTrackInfo (track, this);
        
                            // notify once per chunk
                            if (i == chunk.Length - 1) {
                                conn.CommitTransaction ();
                                contact_track.Save (true);
                            } else {
                                contact_track.Save (false);
                            }
                        }
                    });

                    library_provider.ChunkReady += OnLibraryChunkReady;
                    library_provider.GetChunks (chunk_length);
    
                    download_monitor.Start ();
                
                } else if (state == ContactSourceState.PermissionNotGranted) {
                    SetWaiting ();
                    service.PermissionResponse += OnPermissionResponse;
                    service.RequestPermission ();
                }
            } catch (Exception e) {
                Log.Exception (e);
                ResetState (false);
                SetStatus (Catalog.GetString ("An error occurred while loading data"), true);
            }
        }

//        private void LoadPlaylists ()
//        {
//            if (Contact == null) {
//                throw new InvalidOperationException ("Contact is null.");
//            }
//            
//            //DBusActivity activity = Contact.DispatchManager.Get <DBusActivity> (contact, MetadataProviderService.BusName);
//            LoadPlaylists (current_activity);
//        }
        
        private void LoadPlaylists (DBusActivity activity)
        {
            if (activity == null) {
                throw new ArgumentNullException ("activity");
            } else if (activity.State != ActivityState.Connected) {
                throw new InvalidOperationException (String.Format ("activity state {0} is invalid.", activity.State));
            } else if (state != ContactSourceState.LoadedMetadata) {
                throw new InvalidOperationException (String.Format ("state {0} is invalid.", state));
            }

            try {
                IMetadataProviderService service = activity.GetDBusObject <IMetadataProviderService> (MetadataProviderService.BusName, MetadataProviderService.ObjectPath);
                int [] playlist_ids = service.GetPlaylistIds (LibraryType.Music);
    
                download_monitor.Reset ();
                
                if (playlist_ids.Length == 0) {
                    state = ContactSourceState.Loaded;
                    HideStatus ();
                } else {
                    foreach (int id in playlist_ids) {
                        string playlist_path = service.CreatePlaylistProvider (id).ToString ();
                        IPlaylistProvider playlist_provider = activity.GetDBusObject <IPlaylistProvider>
                        (PlaylistProvider.BusName, playlist_path);

                        Download download = new Download ();
                        download_monitor.Add (playlist_path, download);
                        download_monitor.AssociateObject (playlist_path, new ContactPlaylistSource (playlist_provider.GetName (), this));
                        
                        download.ProcessIncomingPayloads (delegate (object sender, object [] o) {
                            IDictionary <string, object> [] chunk = o as IDictionary <string, object> [];
                            if (chunk == null) {
                                return;
                            }

                            Download d = sender as Download;
                            ContactPlaylistSource source = download_monitor.GetAssociatedObject (d) as ContactPlaylistSource;
                
                            if (source != null) {
                                source.AddTracks (chunk);
                            }

                            ThreadAssist.ProxyToMain (delegate {
                                if (d != null && d.IsFinished) {
                                    Log.DebugFormat ("Download complete for {0}", playlist_path);
                                    AddChildSource (source);
                                    HideStatus ();
                                }
                            });
                        });
                        
                        playlist_provider.ChunkReady += OnPlaylistChunkReady;
                        playlist_provider.GetChunks (chunk_length);
                    }
    
                    download_monitor.Start ();
                }
            } catch (Exception e) {
                Log.Exception (e);
                ResetState (false);
                SetStatus (Catalog.GetString ("An error occurred while loading playlists"), true);
            }
        }
        
        private void ResetState ()
        {
            ResetState (true);
        }
        
        private void ResetState (bool hide_status)
        {
            state = ContactSourceState.Unloaded;
            download_monitor.Reset ();
            
            if (hide_status) {
                HideStatus ();
            }
        }

#endregion

#region Contact Events
        
        private void OnContactUpdated (object sender, ContactStatusEventArgs args)
        {
            this.Name = String.Format ("{0} ({1})", ContactName, ContactStatus);
        }

#endregion        
        
#region Download Events

        private void OnAllDownloadsFinished (object sender, EventArgs args)
        {
            if (state == ContactSourceState.LoadingMetadata) {
                state = ContactSourceState.LoadedMetadata;
                
                SetStatus (Catalog.GetString ("All tracks downloaded. Loading..."), false);
            } else {
                state = ContactSourceState.Loaded;
            }
        }

        private void OnAllDownloadsProcessed (object sender, EventArgs args)
        {
            if (state == ContactSourceState.LoadedMetadata) {

                SetStatus (Catalog.GetString ("Loading playlists"), false);
                
                //FIXME delay required to let tracks save to database
                //after download
                System.Timers.Timer timer = new System.Timers.Timer (1000);
                timer.Elapsed += delegate {
                    try {
                        LoadPlaylists (current_activity);
                    } catch (Exception e) {
                        Log.Exception (e);
                    } finally {
                        timer.Stop ();
                    }
                };
                timer.AutoReset = false;
                timer.Start ();
            }
        }

#endregion        
        
#region Activity Events
        
        private void OnActivityReady (object sender, EventArgs args)
        {
            DBusActivity activity = sender as DBusActivity;
            if (activity == null || !activity.Service.Equals (MetadataProviderService.BusName)) {
                return;
            }
            
            if (Contact != null && Contact.Equals (activity.Contact)) {
                Log.DebugFormat ("ContactSource OnReady for {0}", Contact.Name);

                current_activity = activity;
                
                // TODO decide if this is the right place for this
                // one contact may not stream, so the tube may not be
                // necessary. But, the OnReady and OnPermissionRequired events
                // only get raised for one contact.
                RequestStreamTube ();
                
                try {
                    if (activity.InitiatorHandle != Contact.Connection.SelfHandle) {
                        RegisterActivityServices (current_activity);
                        
                        // tube was not ready at the time user clicked source
                        // so it was put into waiting state
                        if (state == ContactSourceState.Waiting) {
                            LoadData (current_activity);
                        }
                    } else {
                        RegisterActivityServices (current_activity, false);
                        LoadData (current_activity);
                    }
                } catch (Exception e) {
                    Log.Exception (e);
                }
            }
        }

        private void OnActivityClosed (object sender, EventArgs args)
        {
            DBusActivity activity = sender as DBusActivity;
            if (activity == null || !activity.Service.Equals (MetadataProviderService.BusName)) {
                return;
            }

            if (Contact != null && Contact.Equals (activity.Contact)) {
                if (activity.InitiatorHandle != Contact.Connection.SelfHandle) {
                    if (dialog != null) {
                        dialog.Destroy ();
                        dialog = null;
                    }
                    
                    ResetState ();
                } else {
                    StopStreaming (this);
                    
                    // the tube was closed before the library was fully downloaded
                    // this seems to occur randomly
                    if (!download_monitor.ProcessingFinished ()) {
                        SetStatus (Catalog.GetString ("A problem occured while downloading this contact's library"), true);
                    }
                    
                    ResetState (false);
                }
                
                current_activity = null;

            }
        }
        
        private void OnActivityResponseRequired (object sender, EventArgs args)
        {
            DBusActivity activity = sender as DBusActivity;
            if (activity == null || !activity.Service.Equals (MetadataProviderService.BusName)) {
                return;
            }
            
            Log.DebugFormat ("OnActivityResponseRequired from {0} for {1}", activity.Contact.Handle, activity.Contact.Name);
                             
            if (Contact != null && Contact.Equals (activity.Contact) &&
                activity.InitiatorHandle != Contact.Connection.SelfHandle) {
                Log.DebugFormat ("{0} handle {1} accepting tube from ContactSource", Contact.Name, Contact.Handle);
                
                dialog = new ContactRequestDialog (Contact.Name);
                dialog.ShowAll ();
                dialog.Response += delegate (object o, Gtk.ResponseArgs e) {
                    try {
                        if (e.ResponseId == Gtk.ResponseType.Accept) {               
                            activity.Accept ();
                        } else if (e.ResponseId == Gtk.ResponseType.Reject) {
                            activity.Reject ();
                        }
                    } catch (Exception ex) {
                        Log.Exception (ex);
                    }

                    if (dialog !=  null) {
                        dialog.Destroy ();
                        dialog = null;
                    }
                };
                
            }
        }
        
#endregion
        
#region MetadataServiceProvider Events
        
        private void OnPermissionResponse (bool granted)
        {
            if (granted) {
                Log.Debug ("Permission granted");
                state = ContactSourceState.PermissionGranted;
                try {
                    LoadData (current_activity);
                } catch (Exception e) {
                    Log.Exception (e);
                }
            } else {
                Log.Debug ("Permission denied");
                ResetState ();
            }
        }

        private void OnLibraryChunkReady (string object_path, IDictionary<string, object>[] chunk, 
                           long timestamp, int seq_num, int total)
        {
            Log.DebugFormat ("Library Chunk Ready timestamp {0} seq {1} tracks {2} path {3}",
                       timestamp, seq_num, chunk.Length, object_path);
            
            Download current_download = download_monitor.Get (object_path);
            if (current_download != null)  {
                if (!current_download.IsStarted) {
                    Log.Debug ("Initializing download");
                    current_download.Timestamp = timestamp;
                    current_download.TotalExpected = total;
                }

                SetStatus (String.Format (Catalog.GetString ("Loading {0} of {1}"), current_download.TotalDownloaded, total), false);
                
                current_download.UpdateDownload (timestamp, seq_num, chunk.Length, chunk);
            }
        }

        private void OnPlaylistChunkReady (string object_path, IDictionary<string, object>[] chunk, 
                           long timestamp, int seq_num, int total)
        {
            Log.DebugFormat ("Playlist Chunk Ready timestamp {0} seq {1} tracks {2} path {3}",
                       timestamp, seq_num, chunk.Length, object_path);

            Download current_download = download_monitor.Get (object_path);
            if (current_download != null) {            
                if (!current_download.IsStarted) {
                    Log.Debug ("Initializing download");
                    current_download.Timestamp = timestamp;
                    current_download.TotalExpected = total;
                }

                SetStatus (String.Format (Catalog.GetString ("Loading {0} of {1}"), current_download.TotalDownloaded, total), false);
                
                current_download.UpdateDownload (timestamp, seq_num, chunk.Length, chunk);
            }
        }

#endregion
        
    }
}
