/*
* This code is a "cTrader Martingale Trading Strategy".
* Provided by ClickAlgo.com, visit us form all your trading software.
* https://clickalgo.com/
*  
* This program is free software; you can redistribute it and/or
* Modify it under the terms of the GNU General Public License
* version 2 as published by the Free Software Foundation.

* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU General Public License for more details.  
* 
* https://www.gnu.org/licenses/gpl-3.0.en.html
* 
* HOW TO REQUEST NEW FEATURES
* ===========================
* 
* IF YOU WOULD LIKE TO CHANGE OR ADD NEW FEATURES TO THIS CBOT
* 
* EMAIL: development@clickalgo.com
* VISIT: https://clickalgo.com/ctrader-programming
* 
*/

using cAlgo.API;
using cAlgo.API.Internals;
using System;
using System.Collections.Generic;
using System.Linq;

namespace cAlgo
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class ClickAlgoMartingale : Robot
    {
        [Parameter("Initial Quantity (Lots)", DefaultValue = 1, MinValue = 0.01, Step = 0.01, Group = "Risk Management")]
        public double InitialQuantity { get; set; }

        [Parameter("Lots Multiplier", DefaultValue = 2.0, MinValue = 1, Step = 1, Group = "Risk Management")]
        public double Multiplier { get; set; }

        [Parameter("Stop Loss", DefaultValue = 100, MinValue = 1, Step = 1, Group = "Risk Management")]
        public double StopLoss { get; set; }

        [Parameter("Take Profit", DefaultValue = 100, MinValue = 1, Step = 1, Group = "Risk Management")]
        public double TakeProfit { get; set; }

        [Parameter("Equity Stop", DefaultValue = 500, MinValue = 1, Step = 1, Group = "Risk Management")]
        public double EquityStop { get; set; }

        [Parameter("Max Drawdown (%)", DefaultValue = 20, MinValue = 1, Step = 1, Group = "Risk Management")]
        public double MartingaleDrawdown { get; set; }

        [Parameter("Friday Shutdown", DefaultValue = true, Group = "Risk Management")]
        public bool FridayShutdownFlag { get; set; }

        [Parameter("Shutdown Time", DefaultValue = 21, MinValue = 1, Step = 1, Group = "Risk Management")]
        public int EndOfWeekHour { get; set; }

        [Parameter("Number of Candles", DefaultValue = 5, MinValue = 1, Step = 1, Group = "Price Action")]
        public int CandlesNumber { get; set; }

        [Parameter("Candle Size (pips)", DefaultValue = 3, Group = "Price Action")]
        public double MinCandleSize { get; set; }

        [Parameter("Minutes between trades", DefaultValue = 0, MinValue = 1, Step = 1, Group = "Cycle Settings")]
        public int MinutesBetweenTrades { get; set; }

        [Parameter("Cycle Period (Hrs)", DefaultValue = 24, MinValue = 1, Step = 1, Group = "Cycle Settings")]
        public int Cycle { get; set; }

        [Parameter("Cycle Reset?", DefaultValue = true, Group = "Cycle Settings")]
        public bool OverrideCycle { get; set; }

        private double BarArrayAverage;
        private double StaticVolumeMultiplier = 1;
        private double VolumeMultiplier = 1;
        private double MaxProfit = 0;
        private double CycleProfit = 0;
        private double InitialStopLoss;
        private double InitialTakeProfit;
        private double InitialEquity;
        private double MaxEquity;
        private double Drawdown;
        private double MaxDrawdown;
        private double LastCandleSize;
        private Queue<double> BarArray = new Queue<double>();
        private bool TakeProfitFlag = true;
        private bool TrailingStopFlag = false;
        private bool ShuttedDownFlag = false;
        private bool CycleOverridenFlag = false;
        private bool FirstPositionFlag = true;
        private DateTime InitialTime;
        private DateTime CycleEndTime;
        private DateTime TimeForNewPosition;
        private TimeSpan EndOfWeek;
        private Position OpenPosition;

        protected override void OnStart()
        {
            EndOfWeek = TimeSpan.FromHours(EndOfWeekHour) - TimeSpan.FromMinutes(1);
            InitialEquity = Account.Equity;
            InitialStopLoss = StopLoss;
            InitialTakeProfit = TakeProfit;
            InitializeCycle();

            for (int i = 1; i <= CandlesNumber; i++)
            {
                BarArray.Enqueue(GetBarHeight(i));
            }

            ResetDrawdown();
        }

        protected override void OnBar()
        {
            UpdateBars();

            // Closes positions at the end of the week
            if (Server.Time.DayOfWeek == DayOfWeek.Friday && Server.Time.TimeOfDay >= EndOfWeek && FridayShutdownFlag == true)
            {
                if (OpenPosition != null)
                {
                    CloseOpenPosition();
                    ResetTPFlags();
                }
            }
            // Normal trade logic during the week.
            else
            {
                if (ShuttedDownFlag == false && Server.Time >= TimeForNewPosition)
                {
                    //Checking if entry condition has been met...
                    var OverZero = BarArray.Where(x => x > 0);
                    var SubZero = BarArray.Where(x => x < 0);

                    //...for a bullish trend.
                    if (OverZero.Count() == CandlesNumber && OpenPosition == null)
                    {
                        //Checking the trend or countertrend variable.
                        var _TradeType = TradeType.Sell;

                        FilterAndExecuteTrades(_TradeType);
                    }

                    //...for a bearish trend.
                    if (SubZero.Count() == CandlesNumber && OpenPosition == null)
                    {
                        //Checking the trend or countertrend variable.
                        var _TradeType = TradeType.Buy;

                        FilterAndExecuteTrades(_TradeType);
                    }
                    //}
                }
            }
        }

        protected override void OnTick()
        {
            // Closes positions at the end of the week
            if (Server.Time.DayOfWeek == DayOfWeek.Friday && Server.Time.TimeOfDay >= EndOfWeek && FridayShutdownFlag == true)
            {
                if (OpenPosition != null)
                {
                    Print("Closing all positions");
                    CloseOpenPosition();
                    ResetTPFlags();
                }
            }
            // Normal trade logic during the week.
            else
            {
                //Checking the drawdown of the acccount.
                if (Account.Equity > MaxEquity)
                    ResetDrawdown();
                else
                {
                    var CurrentDrawdown = MaxEquity - Account.Equity;
                    Drawdown = CurrentDrawdown > Drawdown ? CurrentDrawdown : Drawdown;
                    if (Drawdown > MaxDrawdown)
                    {
                        CloseOpenPosition();
                        ResetTPFlags();
                        ResetMartingaleVariables();
                        ResetDrawdown();
                    }
                }

                //Checking if the cycle has ended;
                if (Server.Time >= CycleEndTime && CycleOverridenFlag == false)
                {
                    CloseOpenPosition();
                    ResetTPFlags();
                    ResetMartingaleVariables();
                    InitializeCycle();
                }

                // Closing trade logic.
                if (ShuttedDownFlag == false)
                {
                    //Checks if the TS is active.
                    if (TrailingStopFlag == true)
                    {
                        UpdateMaxProfit();
                        UpdateStopLoss();
                    }

                    //Checks if there's an open position.
                    if (OpenPosition != null)
                    {
                        // Activates TS when TP is reached.
                        if (OpenPosition.NetProfit >= TakeProfit && TakeProfitFlag == true)
                        {
                            ActivateTrailingStop();
                        }

                        //Checking the shut down properties.
                        if (CycleProfit + OpenPosition.NetProfit <= -EquityStop)
                        {
                            ShuttedDownFlag = true;
                            CloseOpenPosition();
                            Print("Shutted down until next cycle.");
                        }
                        // Stop Loss logic.
                        else if (OpenPosition.NetProfit <= -StopLoss)
                        {
                            CloseOpenPosition();

                            // If the TP was never reached, the position volume is multiplied.
                            if (TakeProfitFlag == true)
                                ApplyMartingaleEffect();
                            else
                                ResetMartingaleVariables();

                            ResetTPFlags();
                        }
                    }
                }
            }
        }

        private void ActivateTrailingStop()
        {
            TakeProfitFlag = false;
            TrailingStopFlag = true;
            MaxProfit = OpenPosition.NetProfit;
            UpdateStopLoss();
        }

        private DateTime AddWorkDays(DateTime originalDate, int workDays)
        {
            DateTime tmpDate = originalDate;
            while (workDays > 0)
            {
                tmpDate = tmpDate.AddDays(1);
                if (tmpDate.DayOfWeek < DayOfWeek.Saturday && tmpDate.DayOfWeek > DayOfWeek.Sunday)
                    workDays--;
            }
            return tmpDate;
        }

        private DateTime AddWorkHours(DateTime originalDate, int workHours)
        {
            DateTime tmpDate = originalDate;
            while (workHours > 0)
            {
                tmpDate = tmpDate.AddHours(1);
                if (tmpDate.DayOfWeek < DayOfWeek.Saturday && tmpDate.DayOfWeek > DayOfWeek.Sunday)
                    workHours--;
            }
            return tmpDate;
        }

        private DateTime AddWorkMinutes(DateTime originalDate, int workMinutes)
        {
            DateTime tmpDate = originalDate;
            while (workMinutes > 0)
            {
                tmpDate = tmpDate.AddMinutes(1);
                if (tmpDate.DayOfWeek < DayOfWeek.Saturday && tmpDate.DayOfWeek > DayOfWeek.Sunday)
                    workMinutes--;
            }
            return tmpDate;
        }

        private void ApplyMartingaleEffect()
        {
            VolumeMultiplier = Multiplier * StaticVolumeMultiplier;
            StaticVolumeMultiplier = Multiplier * StaticVolumeMultiplier;
            StopLoss = InitialStopLoss * VolumeMultiplier;
            TakeProfit = InitialTakeProfit * VolumeMultiplier;
            FirstPositionFlag = false;
            if (OverrideCycle == true)
                CycleOverridenFlag = true;
        }

        private void CloseOpenPosition()
        {
            if (OpenPosition != null)
            {
                var Result = ClosePosition(OpenPosition);
                if (Result.IsSuccessful)
                {
                    OpenPosition = null;
                    CycleProfit += Result.Position.NetProfit;
                    TimeForNewPosition = AddWorkMinutes(Server.Time, MinutesBetweenTrades);
                }
            }
        }

        private void ExecuteOrder(TradeType _TradeType)
        {
            var Result = ExecuteMarketOrder(_TradeType, Symbol, Symbol.NormalizeVolume(Symbol.QuantityToVolume(InitialQuantity * Multiplier)));
            if (Result.IsSuccessful)
            {
                OpenPosition = Result.Position;
            }
        }

        private void FilterAndExecuteTrades(TradeType _TradeType)
        {
            ExecuteNewOrder(_TradeType);
        }

        private void ExecuteNewOrder(TradeType _TradeType)
        {
            ExecuteOrder(_TradeType);
        }

        private double GetBarHeight(int index)
        {
            double Close = MarketSeries.Close.Last(1);
            double Open = MarketSeries.Open.Last(1);
            LastCandleSize = Close - Open;

            return LastCandleSize;
        }

        private void InitializeCycle()
        {
            InitialTime = Server.Time;
            CycleEndTime = AddWorkHours(InitialTime, Cycle);
            ShuttedDownFlag = false;
            CycleProfit = 0;
            Print("A new cycle began. Ends on {0}", CycleEndTime);
        }

        private void ResetDrawdown()
        {
            MaxEquity = Account.Equity;
            Drawdown = 0;
            MaxDrawdown = MaxEquity * MartingaleDrawdown / 100;

        }

        private void ResetTPFlags()
        {
            TakeProfitFlag = true;
            TrailingStopFlag = false;
        }

        private void ResetMartingaleVariables()
        {
            VolumeMultiplier = 1;
            StaticVolumeMultiplier = 1;
            StopLoss = InitialStopLoss;
            TakeProfit = InitialTakeProfit;
            CycleOverridenFlag = false;
            FirstPositionFlag = true;
        }

        private void UpdateBars()
        {
            BarArray.Dequeue();
            BarArrayAverage = BarArray.Average();
            BarArray.Enqueue(GetBarHeight(1));
        }

        private void UpdateMaxProfit()
        {
            MaxProfit = OpenPosition.NetProfit > MaxProfit ? OpenPosition.NetProfit : MaxProfit;
        }

        private void UpdateStopLoss()
        {
            StopLoss = -MaxProfit + TakeProfit;
        }
    }
}

