// -------------------------------------------------------------------------------------------------
//
//    This code is a cTrader Algo API example.
//
//    This code is intended to be used as a sample and does not guarantee any particular outcome or
//    profit of any kind. Use it at your own risk.
//    
//    This sample adds an active symbol panel tab, and uses ChartIndicators API to show stats about
//    active chart indicators and lets you add and remove indicators to active chart.
//
// -------------------------------------------------------------------------------------------------

using cAlgo.API;

namespace cAlgo.Plugins
{
    [Plugin(AccessRights = AccessRights.None)]
    public class ChartIndicatorsSample : Plugin
    {
        private ChartIndicatorsControl _chartIndicatorsControl;
        
        protected override void OnStart()
        {
            var aspTab = Asp.AddTab("Chart Indicators");
            
            _chartIndicatorsControl = new ChartIndicatorsControl(AlgoRegistry)
            {
                VerticalAlignment = VerticalAlignment.Top
            };

            aspTab.Child = _chartIndicatorsControl;

            SetControlChart();

            ChartManager.ActiveFrameChanged += _ => SetControlChart();
        }

        private void SetControlChart()
        {
            if (ChartManager.ActiveFrame is not ChartFrame chartFrame)
                return;

            _chartIndicatorsControl.Chart = chartFrame.Chart;
        }
    }        
}