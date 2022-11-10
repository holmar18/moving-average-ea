using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None)]
    public class MovingAverageTrendstrat1v101TrendfilterTrailingstop : Robot
    {
        [Output("First", LineColor = "Turquoise")]
        public IndicatorDataSeries Result { get; set; }
        
        [Output("Second", LineColor = "Blue")]
        public IndicatorDataSeries ResultSecond { get; set; }
        
        [Parameter("EMA distance from each other to trade" , DefaultValue = 0.0003, Group = "HSS", MaxValue = 0.1, MinValue = 0.0001, Step = 0.0001)]
        public double EmaDistanceFromEachOther { get; set; }
        
        [Parameter("Slow EMA" , DefaultValue = 20, Group = "HSS", MaxValue = 800, MinValue = 4, Step = 1)]
        public int SlowEma { get; set; }
        
        [Parameter("Fast EMA" , DefaultValue = 50, Group = "HSS", MaxValue = 1000, MinValue = 10, Step = 1)]
        public int FastEma { get; set; }
        
        [Parameter("Stop Loss" , DefaultValue = 9, Group = "HSS", MaxValue = 800, MinValue = 1, Step = 1)]
        public int SL { get; set; }
        
        [Parameter("Take Profit" , DefaultValue = 18, Group = "HSS", MaxValue = 1000, MinValue = 1, Step = 1)]
        public int TP { get; set; }
        
        [Parameter("Trailing stop" , DefaultValue = false, Group = "HSS")]
        public bool trailingStop { get; set; }
        
        private double ma_20;
        private double ma_50;
        private AverageTrueRange atrIndicator;
        
        
        double VolumeUnits;
        double stopLoss;
        int PositionId;
        private TradeType Tradet = TradeType.Sell;
        bool CanTrade = true;
        
        protected override void OnStart()
        {
            ma_20 = Indicators.MovingAverage(Bars.ClosePrices, 10, MovingAverageType.Exponential).Result.LastValue;
            
            
            ma_50 = Indicators.MovingAverage(Bars.ClosePrices, 50, MovingAverageType.Exponential).Result.LastValue;

            
            // Calcualte the Atr
            atrIndicator = Indicators.AverageTrueRange(14, MovingAverageType.Triangular);
        }
        
        
        private void MA_CrossOver_Under()
        {
            // Returns null or an open Position
            var pp = Positions.FirstOrDefault(p => p.Id == PositionId);
            bool longP = EmaRange("LONG");
            bool shortP = EmaRange("SHORT");
            
            if(ma_20 > ma_50 && longP || pp == null && longP) 
            {   
                if(pp == null)
                    Exicute_trade("LONG");
            }
            else if (ma_20 < ma_50 && shortP || pp == null && shortP)
            {
                if(pp == null) 
                    Exicute_trade("SHORT");
            }
            
            Find_distance_between_EMAS(pp);
        }
        
        
        
        private void Exicute_trade(string dir)
        {
            VolumeUnits = Symbol.QuantityToVolumeInUnits(2.00); 
            stopLoss =  atrIndicator.Result.LastValue;
            //Print("ATR: ", atrIndicator.Result.LastValue);
            //Print("SL: ", stopLoss);
            var takeProfit = stopLoss * 1.5;
            
            
            var pos = ExecuteMarketOrder(dir == "LONG" ? TradeType.Buy : TradeType.Sell, SymbolName, VolumeUnits, "MA", SL, TP, "Trade", trailingStop);
            if(pos.IsSuccessful)
            {
                var ind = Bars.Count - 1;
                Color col = dir == "LONG" ? Color.Green : Color.Red;
                var placement = dir == "LONG" ? Bars.HighPrices[ind] + 0.0001 : Bars.LowPrices[ind] - 0.0001;
                Chart.DrawText(string.Format("{0}", ind), string.Format("{0}", ind), ind, placement, col);
                PositionId = pos.Position.Id;
                Tradet = pos.Position.TradeType;
            }

        }
        
        
        private void ClosePos(Position pos)
        {
            // If there is no open position skip closing.
            if(pos != null)
            {
                // Set the label on Previous bar - 1 bc if new trade is entered it will overwrite the label.
                var ind = Bars.Count - 2;
                var h_l_p = pos.TradeType == TradeType.Sell ? Bars.HighPrices[ind] + 0.001 : Bars.LowPrices[ind] - 0.001;
                Color col = pos.TradeType == TradeType.Sell ? Color.AliceBlue : Color.AntiqueWhite;
                
                Chart.DrawText(string.Format("{0}", ind), string.Format("{0}", "TP"), ind, h_l_p, col);
                ClosePosition(pos);
            }      
        }
        
        private bool EmaRange(string dir)
        {
            /*
                Bool dir
                Long
                Short
                
                maxDist = calculate 0.003 % of total
                mx = remove mxDist from 50 ema (the 20 ema cannot be obove it to go into a short trade)
                mxt = add the mxDist to the 50 ema (the 50 cannot be above it for a long trade)
            
            */
            var maxDist = ma_50 * EmaDistanceFromEachOther;
            double mx = (ma_50 - maxDist);
            double mxt = (ma_50 + maxDist);
            // Short and 20 EMA cannot be bigger than 50 - 0.003% to enter short
            if(dir == "SHORT" && mx <= ma_20) 
            {
                return false;
            } 
            // Long and 20 EMA cannot be smaller than 50 + 0,003% to enter lon
            else if(dir == "LONG" && mxt >= ma_20)
            {
                return false;
            }
            return true;
        }
        
        
        
        private void Find_distance_between_EMAS(Position pos)
        {
            if(pos != null)
            {
                var maxDist = ma_50 * EmaDistanceFromEachOther;
                if(pos.TradeType == TradeType.Sell)
                {
                   double mx = (ma_50 - maxDist);
                   // Close short position if EMA 20 goes over 50 EMA - 0.003 %
                   if(mx <= ma_20)
                        ClosePos(pos);
                }
                else if(pos.TradeType == TradeType.Buy)
                {
                   double mx = (ma_50 + maxDist);
                   if(mx >= ma_20)
                        ClosePos(pos);
                }
            }
        }
        


        protected override void OnTick()
        {
            ma_20 = Indicators.MovingAverage(Bars.ClosePrices, SlowEma, MovingAverageType.Exponential).Result.LastValue;
            
            
            ma_50 = Indicators.MovingAverage(Bars.ClosePrices, FastEma, MovingAverageType.Exponential).Result.LastValue;

            
            // Calcualte the Atr
            atrIndicator = Indicators.AverageTrueRange(14, MovingAverageType.Triangular);
            
            MA_CrossOver_Under();
        }


        protected override void OnStop()
        {
            // Handle cBot stop here
        }
        
    }
}