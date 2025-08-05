#region Copyright Kayomani 2011.  Licensed under the GPLv3 (Or later version), Expand for details. Do not remove this notice.

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

namespace FAP.Application.Controllers
{
    /// <summary>
    /// Interface for managing popup windows and tabs
    /// </summary>
    public interface IPopupWindowController
    {
        /// <summary>
        /// Adds a new window/tab with the specified view and title
        /// </summary>
        /// <param name="view">The view to display</param>
        /// <param name="title">The title for the window/tab</param>
        void AddWindow(object view, string title);

        /// <summary>
        /// Closes all popup windows
        /// </summary>
        void Close();

        /// <summary>
        /// Switches to the specified tab
        /// </summary>
        /// <param name="viewModel">The view model to switch to</param>
        void SwitchToTab(object viewModel);

        /// <summary>
        /// Gets or sets the active tab
        /// </summary>
        object ActiveTab { get; set; }

        /// <summary>
        /// Highlights the specified view model
        /// </summary>
        /// <param name="viewModel">The view model to highlight</param>
        void Highlight(object viewModel);

        /// <summary>
        /// Flashes the window if it's not active
        /// </summary>
        void FlashIfNotActive();
    }
} 