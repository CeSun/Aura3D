using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Example.ViewModels;
using Example.Views;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Example
{
    public partial class App : Application
    {
        public static Model? model;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            Task.Run(() =>
            {

                using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/present_11_BACKED.glb")))
                {
                    var m = ModelLoader.LoadGlbModel(stream);

                    var item1 = m.Meshes.First(mesh => mesh.Name == "item1");
                    var item2 = m.Meshes.First(mesh => mesh.Name == "item2");
                    var item3 = m.Meshes.First(mesh => mesh.Name == "item3");
                    var item4 = m.Meshes.First(mesh => mesh.Name == "item4");
                    var item5 = m.Meshes.First(mesh => mesh.Name == "item5");
                    var item6 = m.Meshes.First(mesh => mesh.Name == "item6");

                    var item7List = m.Meshes.Where(mesh => mesh.Name == "item7").ToList();
                    var item7 = item7List.First();



                    m.RemoveChild(item2.Parent, AttachToParentRule.KeepWorld);
                    item2.Parent.RemoveChild(item2, AttachToParentRule.KeepWorld);
                    m.RemoveChild(item3.Parent, AttachToParentRule.KeepWorld);
                    item3.Parent.RemoveChild(item3, AttachToParentRule.KeepWorld);
                    m.RemoveChild(item4.Parent, AttachToParentRule.KeepWorld);
                    item4.Parent.RemoveChild(item4, AttachToParentRule.KeepWorld);
                    m.RemoveChild(item5.Parent, AttachToParentRule.KeepWorld);
                    item5.Parent.RemoveChild(item5, AttachToParentRule.KeepWorld);
                    m.RemoveChild(item6.Parent, AttachToParentRule.KeepWorld);
                    item6.Parent.RemoveChild(item6, AttachToParentRule.KeepWorld);
                    m.RemoveChild(item7.Parent, AttachToParentRule.KeepWorld);
                    item7.Parent.RemoveChild(item7, AttachToParentRule.KeepWorld);
                    item7List[1].Parent.RemoveChild(item7List[1], AttachToParentRule.KeepWorld);
                    item7List[1].Name = "item7_2";
                    item7.AddChild(item7List[1], AttachToParentRule.KeepWorld);


                    item1.AddChild(item2, AttachToParentRule.KeepWorld);
                    item2.AddChild(item3, AttachToParentRule.KeepWorld);
                    item3.AddChild(item4, AttachToParentRule.KeepWorld);
                    item4.AddChild(item5, AttachToParentRule.KeepWorld);
                    item5.AddChild(item6, AttachToParentRule.KeepWorld);
                    item6.AddChild(item7List[0], AttachToParentRule.KeepWorld);

                    model = m;

                }
            });
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewViewModel()
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = new MainViewViewModel()
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

    }
}