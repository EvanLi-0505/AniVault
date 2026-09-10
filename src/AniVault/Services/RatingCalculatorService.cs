using System;
using System.Threading.Tasks;
using System.Windows;
using AniVault.ViewModels;
using AniVault.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AniVault.Services;

/// <summary>
/// Opens the modal rating questionnaire. Lives in the composition root so ViewModels can
/// launch it without referencing a View type.
/// </summary>
public interface IRatingCalculatorService
{
    /// <summary>Returns the score the user chose to assign, or null if they cancelled.</summary>
    Task<double?> RunAsync();
}

public sealed class RatingCalculatorService : IRatingCalculatorService
{
    private readonly IServiceProvider _services;

    public RatingCalculatorService(IServiceProvider services)
    {
        _services = services;
    }

    public Task<double?> RunAsync()
    {
        var viewModel = _services.GetRequiredService<RatingCalculatorViewModel>();
        var window = new RatingCalculatorWindow
        {
            DataContext = viewModel,
            Owner = Application.Current.MainWindow,
        };

        double? assigned = null;
        viewModel.Assigned += score =>
        {
            assigned = score;
            window.DialogResult = true;
        };
        viewModel.Cancelled += () => window.DialogResult = false;

        window.ShowDialog();
        return Task.FromResult(assigned);
    }
}
