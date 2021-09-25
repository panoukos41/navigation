﻿using Flurl;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace P41.Navigation.Host;

/// <summary>
/// A base implementation for the <see cref="INavigationHost"/>.
/// </summary>
public abstract class NavigationHostBase : INavigationHost
{
    private NavigationStack _stack = null!;
    private readonly Subject<INavigationHost> _whenNavigating = new();

    /// <inheritdoc/>
    public int Count => Stack.Count;

    /// <inheritdoc/>
    public IViewFor? CurrentView { get; private set; }

    /// <inheritdoc/>
    public Url? CurrentRequest => Stack.Count is 0 ? null : Stack.Peek();

    /// <inheritdoc/>
    public Interaction<Url, Unit> Push { get; } = new();

    /// <inheritdoc/>
    public Interaction<Unit, Url?> Pop { get; } = new();

    /// <inheritdoc/>
    public Interaction<Unit, bool> ShouldPopRoot { get; } = new();

    /// <inheritdoc/>
    public IObservable<INavigationHost> WhenNavigating => _whenNavigating.AsObservable();

    /// <summary>
    /// The navigation stack.
    /// </summary>
    protected NavigationStack Stack
    {
        get => _stack;
        set
        {
            _stack = value;
            Stack.Change.Subscribe(_ => _whenNavigating.OnNext(this));
        }
    }

    /// <summary>
    /// Initialization of navigation logic.
    /// </summary>
    protected NavigationHostBase()
    {
        Stack = new();
        Push.RegisterHandler(async (input, handled) =>
        {
            if (CurrentRequest == input) return Unit.Default;

            NavigatingFromViewModel();

            Stack.Push(input);
            CurrentView = await PlatformNavigate();

            NavigatedToViewModel();

            return Unit.Default;
        });

        Pop.RegisterHandler(async (input, handled) =>
        {
            if (Stack.Count == 0) throw new InvalidOperationException("There is nothing to pop.");

            if (Stack.Count > 1 || await ShouldPopRoot.Handle(Unit.Default))
            {
                NavigatingFromViewModel();

                var popped = Stack.Pop();
                CurrentView = await PlatformGoBack();

                NavigatedToViewModel();

                return popped;
            }

            return CurrentRequest;
        });

        ShouldPopRoot.RegisterHandler(static c => c.SetOutput(false));
    }

    private void NavigatedToViewModel()
    {
        // Going to the View/ViewModel
        if (CurrentView?.ViewModel is INavigationAware nextVm)
        {
            nextVm.NavigatedTo(CurrentRequest!, this).Subscribe();
        }
    }
    private void NavigatingFromViewModel()
    {
        // Leaving the View/ViewModel
        if (CurrentView?.ViewModel is INavigationAware previusVm)
        {
            previusVm.NavigatingFrom().Subscribe();
        }
    }

    /// <summary>
    /// Called by implementations to set the view model on a View
    /// after its creation.
    /// </summary>
    /// <param name="view"></param>
    protected void SetViewModel(IViewFor? view)
    {
        if (view is { ViewModel: null })
        {
            view.ViewModel = InitializeViewModel();
        }
    }

    /// <summary>
    /// This method is called on the implementation.
    /// </summary>
    /// <returns>The new <see cref="CurrentView"/> object.</returns>
    protected abstract IObservable<IViewFor> PlatformNavigate();

    /// <summary>
    /// This method is called on the implementation.
    /// </summary>
    /// <returns>The new <see cref="CurrentView"/> object.</returns>
    /// <remarks>If we pop the root page null should be returned.</remarks>
    protected abstract IObservable<IViewFor?> PlatformGoBack();

    /// <summary>
    /// Initialize a new ViewModel for the CurrentRequest.
    /// </summary>
    protected abstract object? InitializeViewModel();
}

/// <summary>
/// A base implementation for the <see cref="INavigationHost"/> that
/// takes into consideration the Host and the views that are hosted..
/// </summary>
/// <typeparam name="THost">The type of the host.</typeparam>
/// <typeparam name="TView">The type of the hosted views.</typeparam>
public abstract class NavigationHostBase<THost, TView> : NavigationHostBase
{
    private THost _host = default!;

    /// <summary>
    /// Gets or sets the Host that should be used for the navigation.
    /// Do not leave this null when using the host.
    /// </summary>
    public THost Host
    {
        get => _host ?? throw new ArgumentNullException(nameof(Host), "A Host was not provided for navigation.");
        set => _host = value ?? throw new NullReferenceException("You tried to set a null host.");
    }

    /// <summary>
    /// Initialize a new View for the CurrentRequest.
    /// </summary>
    protected abstract TView InitializeView();
}

/// <summary>
/// Base implementation from which all platform implementations derive.
/// It contains dictionaries with factory methods and already overrides
/// InitliazeViewmodel and InitializeView to use the factory methods.
/// </summary>
/// <typeparam name="THost">The type of the host.</typeparam>
/// <typeparam name="TView">The type of the stored view.</typeparam>
/// <typeparam name="TImplementation">The type inheriting this base.</typeparam>
public abstract class NavigationHostBase<THost, TView, TImplementation> : NavigationHostBase<THost, TView>
    where TImplementation : NavigationHostBase<THost, TView, TImplementation>
{
    /// <summary>
    /// A <see cref="NavigationRoute"/> to Vm/View factories.
    /// </summary>
    private Dictionary<NavigationRoute, (Func<object>? vm, Func<TView> view)> Factories { get; } = new();

    /// <inheritdoc/>
    protected override TView InitializeView()
    {
        var factory = Factories.First(r => r.Key.Match(CurrentRequest!)).Value.view;

        return factory.Invoke();
    }

    /// <inheritdoc/>
    protected override object? InitializeViewModel()
    {
        var factory = Factories.First(r => r.Key.Match(CurrentRequest!)).Value.vm;

        return factory?.Invoke();
    }

    /// <summary>
    /// Map a <see cref="NavigationRoute"/> to factory methods for Vm/View.
    /// </summary>
    /// <param name="route">The rout that coresponds to the Vm/View pair.</param>
    /// <param name="vmFactory">A factory method to create the viewmodel for the route.</param>
    /// <param name="viewFactory">A factory method to create the <typeparamref name="TView"/> for the route.</param>
    /// <returns>The host for further configuration.</returns>
    /// <remarks>
    /// If a route is mapped for a second time it will override the previous route.
    /// A vm factory method can be null in that case only view navigation happens.
    /// </remarks>
    public TImplementation Map(NavigationRoute route, Func<object>? vmFactory, Func<TView> viewFactory)
    {
        Factories[route] = (vmFactory, viewFactory);
        return (TImplementation)this;
    }
}
