﻿namespace UI.Uwp;

public abstract class Page3Base : ReactivePage<Page3ViewModel> { }

public sealed partial class Page3 : Page3Base
{
    public Page3()
    {
        this.InitializeComponent();
    }
}
