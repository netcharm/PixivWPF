using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input.StylusPlugIns;

namespace PixivWPF.Common
{
    public class FakeWindow : Window
    {

    }

    class FakeStylusPlugIn : StylusPlugIn
    {
        public FakeWindow FakeWin { get; }

        public FakeStylusPlugIn(FakeWindow fakeWin)
        {
            FakeWin = fakeWin;
        }

        /// <inheritdoc />
        protected override void OnStylusUp(RawStylusInput rawStylusInput)
        {
            FakeWin.Dispatcher.Invoke(() => FakeWin.Close());
            base.OnStylusUp(rawStylusInput);
        }
    }
}
