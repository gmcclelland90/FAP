#region Copyright Kayomani 2010.  Licensed under the GPLv3 (Or later version), Expand for details. Do not remove this notice.
/**
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or any 
    later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * */
#endregion
using System.Collections.ObjectModel;
using Fap.Foundation.Threading;

namespace Fap.Foundation
{
    /// <summary>
    /// ObservableCollection that marshals CollectionChanged to the UI dispatcher when set.
    /// </summary>
    public class ObservableCollectionEx<T> : ObservableCollection<T>
    {
        public override event System.Collections.Specialized.NotifyCollectionChangedEventHandler CollectionChanged;

        protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            using (BlockReentrancy())
            {
                var eventHandler = CollectionChanged;
                if (eventHandler == null)
                    return;

                var ui = SafeObservableStatic.UiDispatcher;
                foreach (System.Collections.Specialized.NotifyCollectionChangedEventHandler handler in eventHandler.GetInvocationList())
                {
                    if (ui != null && !ui.CheckAccess())
                        ui.Invoke(() => handler(this, e));
                    else
                        handler(this, e);
                }
            }
        }

        public void TriggerCollectionChanged()
        {
            CollectionChanged?.Invoke(this,
                new System.Collections.Specialized.NotifyCollectionChangedEventArgs(
                    System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
        }
    }
}
