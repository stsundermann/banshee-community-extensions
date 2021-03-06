//
// CS_PlayListModel.cs
//
// Authors:
//   Hans Oesterholt <hans@oesterholt.net>
//
// Copyright (C) 2013 Hans Oesterholt
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
using Banshee.Collection;

namespace Banshee.CueSheets
{
	public class CS_PlayListModel : BansheeListModel<CueSheetEntry>
	{
		private CS_PlayList				_pls;

        public CS_PlayListModel () {
			_pls=null;
			Selection=new Hyena.Collections.Selection();
        }

        public override void Clear () {
			// does nothing 
        }
	
        public override void Reload () {
			base.RaiseReloaded ();
        }
		
		public void SetPlayList(CS_PlayList pls) {
			_pls=pls;
			Reload ();
		}
		
		public CS_PlayList PlayList {
			get { return _pls; }
			set { SetPlayList (value); }
		}
	
        public override int Count {
            get { 
				if (_pls==null) { return 0; }
				else { return _pls.Count; }
			}
        }
	
		public override CueSheetEntry this[int index] {
			get {
				return _pls[index];
			} 	
		}
	}
}

