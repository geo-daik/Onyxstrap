using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Onyxstrap.UI.ViewModels.Settings;

namespace Onyxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for OnyxstrapPage.xaml
    /// </summary>
    public partial class OnyxstrapPage
    {
        public OnyxstrapPage()
        {
            DataContext = new OnyxstrapViewModel();
            InitializeComponent();
        }
    }
}
