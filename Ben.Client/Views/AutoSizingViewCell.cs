using System.ComponentModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace Ben.Views;

public class AutoSizingViewCell : ViewCell
{
    INotifyPropertyChanged? _notifyingContext;

    protected override void OnBindingContextChanged()
    {
        Unsubscribe(_notifyingContext);

        base.OnBindingContextChanged();

        _notifyingContext = BindingContext as INotifyPropertyChanged;
        Subscribe(_notifyingContext);
    }

    void Subscribe(INotifyPropertyChanged? notifyingContext)
    {
        if (notifyingContext == null)
        {
            return;
        }

        notifyingContext.PropertyChanged += OnBoundItemPropertyChanged;
    }

    void Unsubscribe(INotifyPropertyChanged? notifyingContext)
    {
        if (notifyingContext == null)
        {
            return;
        }

        notifyingContext.PropertyChanged -= OnBoundItemPropertyChanged;
    }

    void OnBoundItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DeviceInfo.Platform != DevicePlatform.iOS)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(ForceUpdateSize);
    }
}