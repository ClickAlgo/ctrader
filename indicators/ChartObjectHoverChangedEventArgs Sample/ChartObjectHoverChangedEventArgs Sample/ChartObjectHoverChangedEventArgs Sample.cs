// -------------------------------------------------------------------------------------------------
//
//    This code is a cTrader Algo API example.
//
//    This Indicator is intended to be used as a sample and does not guarantee any particular outcome or
//    profit of any kind. Use it at your own risk.
//
// -------------------------------------------------------------------------------------------------

using cAlgo.API;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class ChartObjectHoverChangedEventArgsSample : Indicator
    {
        protected override void Initialize()
        {
            Chart.ObjectHoverChanged += Chart_ObjectHoverChanged;
            ;
        }

        private void Chart_ObjectHoverChanged(ChartObjectHoverChangedEventArgs obj)
        {
            Chart.DrawStaticText("hover", string.Format("Is Object Hovered: {0}", obj.IsObjectHovered), VerticalAlignment.Top, HorizontalAlignment.Right, Color.Red);
        }

        public override void Calculate(int index)
        {
        }
    }
}
