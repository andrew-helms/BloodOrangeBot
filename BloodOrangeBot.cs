using Robocode;
using Robocode.Util;

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;

namespace CAP4053.Student
{
    public class BloodOrangeBot: TeamRobot
    {
        StateMachine fsm;
        String target;
        double tarHead;
        double tarDist;
        int scanCtr;

        public override void Run()
        {
            BodyColor = (Color.FromArgb(209, 0, 28));
            GunColor = (Color.FromArgb(209, 0, 28));
            RadarColor = (Color.FromArgb(224, 17, 95));
            ScanColor = (Color.FromArgb(224, 17, 95));
            BulletColor = (Color.FromArgb(224, 17, 95));

            IsAdjustGunForRobotTurn = (true);

            fsm = new StateMachine();
            fsm.Init(this);

            target = "";

            tarHead = 0;
            tarDist = 0;
            scanCtr = 0;

            //reduces performance
            /*AddCustomEvent(new Condition("nearwall", (c) => 
            {
                bool nearRight = X > BattleFieldWidth * 0.95 && (Heading < 180 && Heading > 0);
                bool nearLeft = X < BattleFieldWidth * 0.05 && (Heading < 360 && Heading > 180);
                bool nearTop = Y > BattleFieldHeight * 0.95 && (Heading < 90 || Heading > 270);
                bool nearBot = Y < BattleFieldHeight * 0.05 && (Heading < 270 && Heading > 90);

                return nearRight || nearLeft || nearTop || nearBot;
            }));*/

            AddCustomEvent(new Condition("timeout", (c) =>
            {
                return scanCtr > 15;
            }));

            while (true)
            {
                fsm.Update();
                scanCtr++;
                Execute();
            }
        }

        public override void OnScannedRobot(ScannedRobotEvent e)
        {
            if (!IsTeammate(e.Name) && (target == e.Name || target == ""))
            {
                target = e.Name;

                if (e.Distance > 0 && GunHeat == 0)
                {
                    SetTurnGunRight(Utils.NormalRelativeAngleDegrees(e.Bearing + (Heading - RadarHeading)) + e.Velocity * Math.Sin(e.HeadingRadians));
                    SetFire(Rules.MAX_BULLET_POWER);
                }

                fsm.EnqueueEvent(e);
                tarDist = e.Distance;
                tarHead = e.Bearing;
                scanCtr = 0;

                SendMessage("CAP4053.Student.key_lime", X.ToString() + "," + Y.ToString() + "," + HeadingRadians.ToString());
            }
        }

        public override void OnHitRobot(HitRobotEvent e)
        {
            fsm.EnqueueEvent(e);
        }

        public override void OnWin(WinEvent e)
        {
            TurnRight(720);
        }

        public override void OnHitWall(HitWallEvent evnt)
        {
            TurnRight(Utils.NormalRelativeAngleDegrees(180 + evnt.Bearing));
            Ahead(100);
        }

        public override void OnCustomEvent(CustomEvent evnt)
        {
            if (evnt.Condition.name == "nearwall")
            {
                
            }

            if (evnt.Condition.name == "timeout")
                fsm.EnqueueEvent(evnt);
        }

        public override void OnMessageReceived(MessageEvent evnt)
        {
            target = evnt.Message.ToString();
        }

        public override void OnRobotDeath(RobotDeathEvent evnt)
        {
            if (evnt.Name == target || evnt.Name == "CAP4053.Student.key_lime")
                target = "";
        }

        private class StateMachine
        {
            List<State> states;
            State current, start;
            BloodOrangeBot bot;

            int input;

            int[,] transition;
            Queue<Event> events;

            public void Init(BloodOrangeBot robot)
            {
                states = new List<State>();

                states.Add(new Attack(robot));
                states.Add(new Search(robot));
                states.Add(new Evade(robot));

                start = states.ElementAt(1);
                current = start;

                start.Enter();

                bot = robot;

                input = 0;

                //States in order: Attack, Search, Evade
                //Inputs in order: Scanned, HitRobot, Timeout
                transition = new int [3,3]{
                    { 0,0,1},
                    { 0,0,1},
                    { 1,1,1}
                };

                events = new Queue<Event>();
            }

            public void Update()
            {
                if (events.Count() > 0)
                {
                    Event e = events.Dequeue();

                    if (e is ScannedRobotEvent)
                        input = 0;
                    if (e is HitRobotEvent)
                        input = 1;
                    if (e is CustomEvent) //timeout
                        input = 2;

                    Transition();
                }

                Console.WriteLine(events.Count());

                current.Update();
            }

            public void Shutdown()
            {
                states.Clear();
                events.Clear();
            }

            public void EnqueueEvent(Event e)
            {
                events.Enqueue(e);
            }

            void Transition()
            {
                State newState = states.ElementAt(transition[states.IndexOf(current), input]);

                if (newState != current)
                {
                    current.Exit();
                    current = newState;
                    current.Enter();
                }
            }

            public abstract class State
            {
                public abstract void Enter();

                public abstract void Update();

                public abstract void Exit();
            }

            private class Attack : State
            {
                BloodOrangeBot bot;
                Random rand;
                int dir;
                double centerX;
                double centerY;

                public Attack(BloodOrangeBot robot)
                {
                    bot = robot;
                    rand = new Random();
                    dir = 1;
                    centerX = bot.BattleFieldWidth / 2;
                    centerY = bot.BattleFieldHeight / 2;
                }

                override public void Enter()
                {

                }

                override public void Update()
                {
                    double temp = rand.NextDouble();
                    if (temp < 0.15)
                        dir *= -1;

                    double dist = Math.Sqrt(Math.Pow((bot.X - centerX),2) + Math.Pow((bot.Y - centerY),2));
                    double centerVector = Math.Atan2((bot.Y - centerY), (bot.X - centerX)) * (180 / Math.PI);

                    bot.SetTurnGunRight(Utils.NormalRelativeAngleDegrees(bot.tarHead + (bot.Heading - bot.GunHeading)));
                    double circleTrack = (90 - Math.Abs(bot.tarHead)) * -1 * Math.Sign(bot.tarHead == 0 ? 1 : bot.tarHead);
                    double distPID = (bot.tarDist - 400) * dir * 0.15 * Math.Sign(bot.tarHead == 0 ? 1 : bot.tarHead);
                    double centerForce = 0;//dist / 900 * (centerVector - bot.Heading); reduces performance
                    bot.SetTurnRight(Utils.NormalRelativeAngleDegrees(circleTrack + distPID + centerForce));
                    bot.SetAhead(25 * dir);
                }

                override public void Exit()
                {

                }
            }

            private class Search : State
            {
                BloodOrangeBot bot;

                public Search(BloodOrangeBot robot)
                {
                    bot = robot;
                }

                override public void Enter()
                {

                }

                override public void Update()
                {
                    bot.SetTurnGunRight(20);
                    bot.SetAhead(10);
                    bot.SetTurnRight(0);
                }

                override public void Exit()
                {

                }
            }

            private class Evade : State
            {
                BloodOrangeBot bot;

                public Evade(BloodOrangeBot robot)
                {
                    bot = robot;
                }

                override public void Enter()
                {

                }

                override public void Update()
                {
                    bot.SetTurnGunRight(0);
                    bot.SetAhead(0);
                    bot.SetTurnRight(0);
                }

                override public void Exit()
                {

                }
            }
        }
    }

}
