using System;

namespace Ben.Services;

public interface IAlertService
{
    Task ShowErrorAlertAsync(string title, string message, string cancel = "OK");
}

public class AlertService : IAlertService
{
    public Task ShowErrorAlertAsync(string title, string message, string cancel = "OK")
        => Application.Current!.Windows[0].Page!.DisplayAlertAsync(title, message, cancel);
}

