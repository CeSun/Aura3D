using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace Example.ViewModels
{
    public partial class MainViewViewModel : ViewModelBase
    {
        public MainViewViewModel() 
        {

            WeakReferenceMessenger.Default.Register<MainViewViewModel, MenuViewModel, string>(this, "JumpTo", OnNavigation);
            _menus = [
                new MenuViewModel {
                    Title = "Base Geometries",
                    ViewModel = new BaseGeometriesViewModel()
                },
                new MenuViewModel {
                    Title = "Load Model File",
                    ViewModel = new ModelPreviewViewModel()
                },
                new MenuViewModel {
                    Title = "Animation",
                    ViewModel = new AnimationFeaturesViewModel()
                },
                new MenuViewModel {
                    Title = "Robotic Arm",
                    ViewModel = new RoboticArmViewModel()
                },
                new MenuViewModel {
                    Title = "PBR RenderPipeline",
                    ViewModel = new PbrViewModel()
                },
                new MenuViewModel {
                    Title = "Cel Shading",
                    ViewModel = new CelShadingViewModel()
                },
                new MenuViewModel {
                    Title = "Cel Shading Material Editor",
                    ViewModel = new CelShadingMaterialEditorViewModel()
                },
                new MenuViewModel {
                    Title = "Rendering Performance",
                    ViewModel = new RenderingPerformanceViewModel()
                },
                new MenuViewModel {
                    Title = "Particle Editor",
                    ViewModel = new ParticleEditorViewModel()
                },
                new MenuViewModel {
                    Title = "Point Cloud",
                    ViewModel = new PointCloudViewModel()
                },
                new MenuViewModel {
                    Title = "Primitive Types",
                    ViewModel = new PrimitiveTypeViewModel()
                },
                new MenuViewModel {
                    Title = "Debug Test",
                    ViewModel = new DebugTestViewModel()
                },
                new MenuViewModel {
                    Title = "Cascaded Shadow Maps",
                    ViewModel = new CascadedShadowMapsViewModel()
                }
            ];
            OnNavigation(this, _menus.First());
        }


        private void OnNavigation(MainViewViewModel vm, MenuViewModel menuItem)
        {
            Content =  menuItem.ViewModel;

        }


        [ObservableProperty] 
        private object? _content;

        [ObservableProperty]
        private ObservableCollection<MenuViewModel> _menus;

        [ObservableProperty]
        private string _greeting = "Welcome to Avalonia!";
    }
}
