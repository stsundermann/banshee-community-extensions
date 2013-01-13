using System;
using Banshee.Configuration;
using Banshee.Collection;
using Banshee.Gui;
using Banshee.Base;
using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Sources.Gui;
using Banshee.Preferences;
using Gdk;

namespace Banshee.CueSheets
{
	public class CueSheetsPrefs : IDisposable
	{
		private SourcePage 				source_page;
		private Section		    		basedir_section;
		private CueSheetsSource 		_source;
		private Gtk.FileChooserButton   _btn;
		
		public CueSheetsPrefs (CueSheetsSource source) {
			var service = ServiceManager.Get<PreferenceService>();
			
			if (service==null) {
				return;
			}
			
			_source=source;
			source_page = new SourcePage(source);
			basedir_section=new Section("cuesheets-basedir","CueSheet Music Directory:",20);
			source_page.Add (basedir_section);
			
			string dir=source.getCueSheetDir();
			Gtk.Label lbl=new Gtk.Label("CueSheet Music Directory:");
			Gtk.FileChooserButton btn=new Gtk.FileChooserButton("CueSheet Music Directory:",Gtk.FileChooserAction.SelectFolder);
			btn.SelectFilename (dir);
			Gtk.HBox box=new Gtk.HBox();
			box.Add (lbl);
			box.Add (btn);
			box.ShowAll ();
			btn.FileSet+=new EventHandler(EvtDirSet);
			_btn=btn;
			
			Console.WriteLine (_source);
			
			Gtk.VBox vb=new Gtk.VBox();
			vb.PackStart (box,false,false,0);
			
			Gtk.Image icn_about=new Gtk.Image(Gtk.Stock.About,Gtk.IconSize.Button);
			Gtk.Button about=new Gtk.Button(icn_about);
			about.Clicked+=new EventHandler(handleAbout);
			Gtk.HBox hb=new Gtk.HBox();
			Gtk.Label _about=new Gtk.Label("About the CueSheet extension");
			hb.PackEnd (about,false,false,0);
			hb.PackEnd (_about,false,false,5);
			vb.PackStart (hb,false,false,0);
			
			Gtk.HBox hb1=new Gtk.HBox();
			vb.PackEnd (hb1,true,true,0);
			
			vb.ShowAll ();
			
			source_page.DisplayWidget = vb;
			
		}
		
		public void EvtDirSet(object sender,EventArgs a) {
			string dir=_btn.Filename;
			_source.setCueSheetDir(dir);			
		}
		
		public string PageId {
            get { return source_page.Id; }
        }
		
	    #region IDisposable implementation
        public void Dispose ()
        {
            //throw new NotImplementedException ();
        }
        #endregion
		
		public void handleAbout(object sender,EventArgs a) {
			Gtk.AboutDialog ab=new Gtk.AboutDialog();
			//ab.Title="About the CueSheets extension";
			ab.Authors=new string[] {"Hans Oesterholt"};
			ab.Authors[0]="Hans Oesterholt";
			ab.Version="0.0.5 (2013-01-12)";
			ab.Comments="CueSheets is an extension that allows you to play music from cuesheets in banshee";
			ab.Website="http://oesterholt.net?env=data&page=banshee-cuesheets";
			ab.Run ();
			ab.Destroy ();
		}
				
		
    }
}
