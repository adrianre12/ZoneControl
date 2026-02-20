using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRageMath;
using ZoneControl.Spawner;

namespace ZoneControl.NotificationBlock
{
    internal class ScreenNotification : ScreenBase
    {
        private const long TicksPerSecond = 10000000L;
        private const long TicksPerMin = 60 * TicksPerSecond;
        private const long TicksPerHour = 60 * TicksPerMin;
        //private const float SecondsPerTick = 1.0f / TicksPerSecond;


        private readonly Color GreenCRT = new Color(51, 255, 0);

        //private Stopwatch stopwatch = new Stopwatch();

        internal bool Dirty;

        internal ScreenNotification() { }
        internal ScreenNotification(IMyTextSurfaceProvider surfaceProvider, int index)
        {
            base.Init(surfaceProvider, index);
            DefaultRotationOrScale = 1.25f;
            BackgroundColor = Color.Black;//.MidnightBlue;
            DefaultColor = GreenCRT;
        }

        internal void Refresh(List<SummaryItem> selected)
        {
            var frame = GetFrame(BackgroundColor);
            var positionTop = new Vector2(5, 5);
            var positionList = new Vector2(5, 75);
            var positionBtm = new Vector2(5, 345);

            var positionTab1 = new Vector2(300, 0);
            var positionTab2 = new Vector2(475, 0);

            /*
                        var position = new Vector2(5, 5);
                        for (int x = 0; x < viewport.Width; x += 50)
                            frame.Add(NewTextSprite("_", new Vector2(position.X + x, position.Y)));
                        for (int y = 0; y < 20; ++y)
                        {
                            frame.Add(NewTextSprite($"{y}", position));
                            position.Y += LineSpaceing;
                        }
            */

            frame.Add(NewTextSprite("Scanning for Anomalies:", positionTop));


            long now = DateTime.Now.Ticks;

            foreach (var item in selected)
            {
                long ticksLeft = item.RemoveAt - now;
                if (ticksLeft < 0)
                    continue;

                frame.Add(NewTextSprite(item.Name, positionList));
                frame.Add(NewTextSprite(FormatHHHHMM(ticksLeft), positionTab1 + positionList));
                frame.Add(NewTextSprite(">", positionTab2 + positionList));
                positionList.Y += 90;
            }

            frame.Add(NewTextSprite("Press button to save GPS", positionBtm));
            //frame.Add(NewTextSprite($"{RunInfo.AvailableUranium}", position + positionTab1, Color.Green));

            Dirty = false;
            frame.Dispose();
        }

        private String FormatHHHHMM(long ticks)
        {
            long hrs = ticks * 1 / TicksPerHour;
            long mins = (ticks - (hrs * TicksPerHour)) * 1 / TicksPerMin;
            return $"{hrs}:{mins:00}";
        }
    }
}
