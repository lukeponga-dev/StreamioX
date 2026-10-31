using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StreamioX.App.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels. Provides common functionality for 
    /// notifying the UI of property changes via INotifyPropertyChanged.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Event triggered when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Checks if a property's value has changed and, if so, updates the backing field 
        /// and raises the PropertyChanged event.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="backingStore">Reference to the property's backing field.</param>
        /// <param name="value">The new value to set.</param>
        /// <param name="propertyName">The name of the property (automatically resolved by the compiler).</param>
        /// <returns>True if the value was changed; otherwise, false.</returns>
        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (Equals(backingStore, value)) return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Manually raises the PropertyChanged event for a specific property.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}