using UltimoRound.Combat;
using UnityEngine;

namespace UltimoRound.Presentation
{
    /// <summary>Poses-chave da animação procedural (placeholder até haver animações humanoides autorizadas).</summary>
    public static class PoseLibrary
    {
        public static FighterPose Guard(GuardStyle s)
        {
            switch (s)
            {
                case GuardStyle.Brawler:
                    return new FighterPose { HipY = -0.1f, Lean = 14, Head = -4, FShoulder = 45, FElbow = 85, RShoulder = 30, RElbow = 100, FHip = 22, FKnee = 24, RHip = -18, RKnee = 26, FArmOut = 12, RArmOut = 12 };
                case GuardStyle.Agile:
                    return new FighterPose { HipY = -0.12f, Lean = 12, Head = -6, FShoulder = 62, FElbow = 118, RShoulder = 52, RElbow = 128, FHip = 20, FKnee = 32, RHip = -20, RKnee = 36 };
                case GuardStyle.KickSpecialist:
                    return new FighterPose { HipY = -0.04f, Lean = 3, Head = -3, FShoulder = 38, FElbow = 95, RShoulder = 26, RElbow = 112, FHip = 26, FKnee = 8, RHip = -12, RKnee = 12 };
                default:
                    return new FighterPose { HipY = -0.06f, Lean = 8, Head = -5, FShoulder = 56, FElbow = 108, RShoulder = 42, RElbow = 122, FHip = 18, FKnee = 16, RHip = -14, RKnee = 20 };
            }
        }

        public static FighterPose Block(GuardStyle s, bool crouch)
        {
            var p = crouch ? Crouch(s) : Guard(s);
            p.FShoulder = 72; p.FElbow = 138; p.RShoulder = 66; p.RElbow = 142;
            p.Head = -12; p.Lean += 4; p.FArmOut = 4; p.RArmOut = 4;
            return p;
        }

        public static FighterPose Crouch(GuardStyle s)
        {
            var p = Guard(s);
            p.HipY = -0.42f; p.Lean = 22;
            p.FHip = 72; p.FKnee = 118; p.RHip = 28; p.RKnee = 122;
            return p;
        }

        public static FighterPose Air(GuardStyle s)
        {
            var p = Guard(s);
            p.FHip = 62; p.FKnee = 100; p.RHip = 28; p.RKnee = 95; p.Lean = 6;
            return p;
        }

        public static FighterPose Squat(GuardStyle s)
        {
            var p = Guard(s);
            p.HipY = -0.16f; p.FKnee += 30; p.RKnee += 30; p.FHip += 15; p.RHip += 10;
            return p;
        }

        public static FighterPose Dash(GuardStyle s, bool back)
        {
            var p = Guard(s);
            if (back) { p.Lean = -8; p.FHip = 40; p.FKnee = 40; p.RHip = -5; p.RKnee = 60; p.HipY = 0.04f; }
            else { p.Lean = 26; p.HipX = 0.1f; p.FHip = 40; p.FKnee = 50; p.RHip = -35; p.RKnee = 30; }
            return p;
        }

        public static FighterPose Hitstun(GuardStyle s, bool low, bool crouch)
        {
            var p = crouch ? Crouch(s) : Guard(s);
            if (low) { p.Lean += 18; p.Head = 10; }
            else { p.Lean = -20; p.Head = 22; p.HipX = -0.08f; }
            p.FShoulder = 25; p.FElbow = 60; p.RShoulder = 10; p.RElbow = 50;
            p.FArmOut = 18; p.RArmOut = 18;
            return p;
        }

        public static FighterPose Launched(GuardStyle s)
        {
            return new FighterPose { Lean = -15, Head = 25, FShoulder = 120, FElbow = 30, RShoulder = 150, RElbow = 20, FHip = 30, FKnee = 60, RHip = 10, RKnee = 40, FArmOut = 35, RArmOut = 35, BodyRoll = 35 };
        }

        public static FighterPose Lying(float hipHeight, bool ko)
        {
            return new FighterPose
            {
                HipY = -hipHeight + 0.14f, BodyRoll = 88, Head = ko ? 10 : 0,
                FShoulder = ko ? 150 : 120, FElbow = 15, RShoulder = ko ? 170 : 140, RElbow = 25,
                FHip = 8, FKnee = ko ? 5 : 35, RHip = -4, RKnee = ko ? 10 : 20, FArmOut = 20, RArmOut = 30,
            };
        }

        public static FighterPose Victory(GuardStyle s, float t)
        {
            float bob = Mathf.Sin(t * 6f);
            switch (s)
            {
                case GuardStyle.Brawler: // bate no peito
                    return new FighterPose { Lean = -6, Head = -10, FShoulder = 60 + bob * 15, FElbow = 120, RShoulder = 170, RElbow = 30, FHip = 10, RHip = -10, FKnee = 8, RKnee = 8, RArmOut = 20, HipY = -0.02f };
                case GuardStyle.Agile: // pulinhos com punho para o alto
                    return new FighterPose { HipY = Mathf.Abs(bob) * 0.08f, Lean = -4, Head = -14, FShoulder = 175, FElbow = 10, RShoulder = 40, RElbow = 130, FHip = 15, FKnee = 25, RHip = -8, RKnee = 30, FArmOut = 10 };
                case GuardStyle.KickSpecialist: // chute alto lento e pose
                    return new FighterPose { Lean = -10, Head = -6, FShoulder = 20, FElbow = 30, RShoulder = 160, RElbow = 15, FHip = 110 + bob * 6, FKnee = 0, RHip = -5, RKnee = 4, RArmOut = 25 };
                default: // braços erguidos
                    return new FighterPose { Lean = -6, Head = -12 + bob * 3, FShoulder = 165, FElbow = 25, RShoulder = 160, RElbow = 25, FHip = 12, RHip = -10, FKnee = 8, RKnee = 8, FArmOut = 18, RArmOut = 18, HipY = Mathf.Abs(bob) * 0.03f };
            }
        }

        public static FighterPose Intro(GuardStyle s, float t)
        {
            var g = Guard(s);
            switch (s)
            {
                case GuardStyle.Brawler:
                    g.RShoulder = 90 + Mathf.Sin(t * 8f) * 20; g.RElbow = 130; g.Head = -14; g.Lean = 4; return g;
                case GuardStyle.Agile:
                    g.HipY += Mathf.Abs(Mathf.Sin(t * 9f)) * 0.07f; return g;
                case GuardStyle.KickSpecialist:
                    g.FHip = 60 + Mathf.Sin(t * 3f) * 50; g.FKnee = 10; g.Lean = -6; return g;
                default:
                    g.FShoulder = 70 + Mathf.Max(0, Mathf.Sin(t * 10f)) * 25; g.FElbow = 60 + Mathf.Max(0, -Mathf.Sin(t * 10f)) * 50; return g;
            }
        }

        /// <summary>Pose de preparação (windup) e de extensão (strike) de um golpe.</summary>
        public static void Attack(AnimPose kind, GuardStyle s, out FighterPose windup, out FighterPose strike)
        {
            var g = Guard(s);
            var c = Crouch(s);
            windup = g;
            strike = g;
            switch (kind)
            {
                case AnimPose.Jab:
                    windup.FShoulder = 50; windup.FElbow = 125; windup.Twist = -10;
                    strike.FShoulder = 90; strike.FElbow = 2; strike.Twist = 14; strike.Lean = 12; strike.HipX = 0.04f;
                    break;
                case AnimPose.Straight:
                    windup.RShoulder = 35; windup.RElbow = 128; windup.Twist = -18;
                    strike.RShoulder = 92; strike.RElbow = 0; strike.Twist = 34; strike.Lean = 16; strike.HipX = 0.1f; strike.RHip = -26; strike.RKnee = 4;
                    break;
                case AnimPose.Hook:
                    windup.RShoulder = 70; windup.RElbow = 90; windup.Twist = -28; windup.RArmOut = 40;
                    strike.RShoulder = 88; strike.RElbow = 80; strike.RArmOut = 75; strike.Twist = 40; strike.Lean = 14; strike.HipX = 0.06f;
                    break;
                case AnimPose.Uppercut:
                    windup.HipY = -0.22f; windup.RShoulder = 10; windup.RElbow = 95; windup.Lean = 20; windup.FKnee += 25; windup.RKnee += 25;
                    strike.HipY = 0.06f; strike.RShoulder = 162; strike.RElbow = 30; strike.Lean = -10; strike.Head = -16; strike.FHip = 8; strike.FKnee = 4; strike.RHip = -6; strike.RKnee = 4; strike.Twist = 20;
                    break;
                case AnimPose.BodyKick:
                    windup.FHip = 45; windup.FKnee = 110; windup.Lean = -4;
                    strike.FHip = 88; strike.FKnee = 2; strike.Lean = -18; strike.RHip = -4; strike.RKnee = 6; strike.FShoulder = 30; strike.RShoulder = -15; strike.RElbow = 60;
                    break;
                case AnimPose.HighKick:
                    windup.FHip = 50; windup.FKnee = 115; windup.Lean = -8;
                    strike.FHip = 128; strike.FKnee = 0; strike.Lean = -32; strike.HipY = 0.02f; strike.RHip = -2; strike.RKnee = 4; strike.FShoulder = 20; strike.RShoulder = -25;
                    break;
                case AnimPose.LowKick:
                    windup = c; windup.FHip = 60; windup.FKnee = 120;
                    strike = c; strike.FHip = 80; strike.FKnee = 0; strike.HipY = -0.38f; strike.Lean = 26; strike.RHip = 35; strike.RKnee = 120;
                    break;
                case AnimPose.Sweep:
                    windup = c; windup.Lean = 30; windup.FHip = 40;
                    strike = c; strike.HipY = -0.55f; strike.Lean = 32; strike.FHip = 82; strike.FKnee = 0; strike.RHip = 45; strike.RKnee = 145; strike.FShoulder = -20; strike.RShoulder = -30;
                    break;
                case AnimPose.CrouchJab:
                    windup = c; windup.FShoulder = 45; windup.FElbow = 125;
                    strike = c; strike.FShoulder = 86; strike.FElbow = 0; strike.Lean = 16; strike.Twist = 12;
                    break;
                case AnimPose.AirKick:
                    windup = Air(s);
                    strike = Air(s); strike.FHip = 78; strike.FKnee = 4; strike.RHip = 20; strike.RKnee = 110; strike.Lean = -14;
                    break;
                case AnimPose.AirPunch:
                    windup = Air(s); windup.FShoulder = 150; windup.FElbow = 120;
                    strike = Air(s); strike.FShoulder = 95; strike.FElbow = 135; strike.Lean = 28; strike.RShoulder = 20;
                    break;
                case AnimPose.Grab:
                    windup.FShoulder = 65; windup.FElbow = 60; windup.RShoulder = 60; windup.RElbow = 60;
                    strike.FShoulder = 82; strike.FElbow = 28; strike.RShoulder = 78; strike.RElbow = 32; strike.Lean = 16; strike.HipX = 0.08f;
                    break;
                case AnimPose.DashPunch:
                    windup.Lean = 18; windup.RShoulder = 30; windup.RElbow = 130; windup.Twist = -20;
                    strike.RShoulder = 96; strike.RElbow = 0; strike.Lean = 30; strike.HipX = 0.18f; strike.RHip = -40; strike.RKnee = 10; strike.FHip = 38; strike.FKnee = 42; strike.Twist = 30;
                    break;
                case AnimPose.SpinKick:
                    windup.Twist = -70; windup.Lean = 14; windup.FHip = 30; windup.FKnee = 90;
                    strike.FHip = 102; strike.FKnee = 0; strike.Lean = -36; strike.Twist = 30; strike.RHip = -2; strike.RKnee = 6; strike.FShoulder = 10; strike.RShoulder = -30;
                    break;
                case AnimPose.Headbutt:
                    windup.Lean = 32; windup.HipY = -0.2f; windup.Head = 20;
                    strike.Lean = -14; strike.HipY = 0.1f; strike.Head = -30; strike.FShoulder = -10; strike.RShoulder = -15; strike.FElbow = 40; strike.RElbow = 40;
                    break;
                case AnimPose.Stomp:
                    windup.FHip = 75; windup.FKnee = 105; windup.Lean = -6;
                    strike.FHip = 48; strike.FKnee = 0; strike.Lean = 8; strike.HipX = 0.05f;
                    break;
                case AnimPose.Shoulder:
                    windup.Lean = 20; windup.Twist = -40; windup.FShoulder = 15; windup.FElbow = 70;
                    strike.Lean = 36; strike.HipX = 0.22f; strike.Twist = -38; strike.FShoulder = 10; strike.FElbow = 60; strike.RShoulder = 40; strike.RHip = -38; strike.FHip = 40; strike.FKnee = 40;
                    break;
                case AnimPose.Axe:
                    windup.FHip = 150; windup.FKnee = 2; windup.Lean = -26; windup.RKnee = 8;
                    strike.FHip = 40; strike.FKnee = 0; strike.Lean = 16; strike.RKnee = 10;
                    break;
                case AnimPose.Overhead:
                    windup.FShoulder = 172; windup.FElbow = 40; windup.RShoulder = 166; windup.RElbow = 40; windup.Lean = -16;
                    strike.FShoulder = 72; strike.FElbow = 10; strike.RShoulder = 70; strike.RElbow = 10; strike.Lean = 32; strike.HipY = -0.1f;
                    break;
                case AnimPose.Flurry:
                case AnimPose.Signature:
                    windup.Lean = 18; windup.Twist = -20; windup.HipY = -0.1f;
                    strike.FShoulder = 90; strike.FElbow = 4; strike.Lean = 18; strike.HipX = 0.08f; strike.Twist = 20;
                    break;
            }
        }
    }
}
