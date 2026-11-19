using System.Collections.Generic;
using UltimoRound.Combat;
using UnityEngine;

namespace UltimoRound.Data
{
    /// <summary>
    /// Valores iniciais (hipóteses de balanceamento) do elenco. Usado pela ferramenta de editor para gerar os
    /// ScriptableObjects em Assets/_Game/Data e pelos testes para criar instâncias em memória.
    /// Depois de gerados, os assets são a fonte de verdade em runtime.
    /// </summary>
    public static class DefaultRoster
    {
        public static readonly string[] FighterIds = { "giovani", "brutus", "faisca", "vertice" };

        public static CombatConfig CreateConfig() => ScriptableObject.CreateInstance<CombatConfig>();

        public static FighterDefinition Create(string id)
        {
            switch (id)
            {
                case "giovani": return Giovani();
                case "brutus": return Brutus();
                case "faisca": return Faisca();
                case "vertice": return Vertice();
                default: return null;
            }
        }

        public static List<FighterDefinition> CreateAll()
        {
            var list = new List<FighterDefinition>();
            foreach (var id in FighterIds)
            {
                var f = Create(id);
                if (f != null) list.Add(f);
            }
            return list;
        }

        // ------------------------------------------------------------------ BUILDER

        sealed class MoveSpec
        {
            readonly MoveDefinition m;

            public MoveSpec(string fighterId, MoveSlot slot, string name, string notation)
            {
                m = ScriptableObject.CreateInstance<MoveDefinition>();
                m.Id = fighterId + "_" + slot.ToString().ToLowerInvariant();
                m.name = m.Id;
                m.Slot = slot;
                m.DisplayName = name;
                m.Notation = notation;
                m.CancelInto = new MoveSlot[0];
            }

            public MoveSpec Frames(int startup, int active, int recovery) { m.Startup = startup; m.Active = active; m.Recovery = recovery; return this; }
            public MoveSpec Dmg(int damage, int hitstun, int blockstun, int hitstop = 7) { m.Damage = damage; m.Hitstun = hitstun; m.Blockstun = blockstun; m.Hitstop = hitstop; return this; }
            public MoveSpec Lv(AttackLevel level) { m.Level = level; return this; }
            public MoveSpec Hit(float x, float y, float w, float h) { m.Hitbox = new Box(x, y, w, h); return this; }
            public MoveSpec Push(float hit, float block) { m.PushbackHit = hit; m.PushbackBlock = block; return this; }
            public MoveSpec Energy(int gain, int cost = 0) { m.EnergyGain = gain; m.EnergyCost = cost; return this; }
            public MoveSpec Cancel(params MoveSlot[] slots) { m.CancelInto = slots; return this; }
            public MoveSpec NoBlockCancel() { m.CancelOnBlock = false; return this; }
            public MoveSpec Kd(KnockdownType k, float vx = 2.5f, float vy = 6f) { m.Knockdown = k; m.LaunchVelocity = new Vector2(vx, vy); return this; }
            public MoveSpec Bounce() { m.RopeBounce = true; return this; }
            public MoveSpec Moves(float vel, int from, int to) { m.ForwardVelocity = vel; m.MoveStartTick = from; m.MoveEndTick = to; return this; }
            public MoveSpec Multi(int hits, int interval) { m.Hits = hits; m.HitInterval = interval; return this; }
            public MoveSpec Air(int landing) { m.RequiresAir = true; m.LandingRecovery = landing; return this; }
            public MoveSpec Inv(InvulnType t, int from, int to) { m.Invuln = t; m.InvulnStart = from; m.InvulnEnd = to; return this; }
            public MoveSpec Throw(float range, bool swap) { m.Level = AttackLevel.Throw; m.ThrowRange = range; m.ThrowSwapsSides = swap; m.ExtendHurtbox = false; return this; }
            public MoveSpec Look(AnimPose pose, SfxKind sfx) { m.Pose = pose; m.HitSfx = sfx; return this; }
            public MoveSpec Special() { m.IsSpecial = true; return this; }
            public MoveSpec Desc(string d) { m.Description = d; return this; }
            public MoveDefinition Build() => m;
        }

        static FighterDefinition NewFighter(string id, string name)
        {
            var f = ScriptableObject.CreateInstance<FighterDefinition>();
            f.Id = id;
            f.name = id;
            f.DisplayName = name;
            return f;
        }

        static ComboRecipe Combo(string name, string notation, string notes, params MoveSlot[] seq) =>
            new ComboRecipe { Name = name, Notation = notation, Notes = notes, Sequence = seq };

        static FighterPalette Pal(string name, Color primary, Color secondary, Color skin, Color accent, Color hair) =>
            new FighterPalette { Name = name, Primary = primary, Secondary = secondary, Skin = skin, Accent = accent, Hair = hair };

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        // ------------------------------------------------------------------ GIOVANI

        static FighterDefinition Giovani()
        {
            const string id = "giovani";
            var f = NewFighter(id, "Giovani");
            f.Epithet = "O Desafiante";
            f.Style = "Kickboxing equilibrado";
            f.Summary = "Socos rápidos, chutes de média distância e um avanço ofensivo curto. Bom para aprender o jogo.";
            f.Weakness = "Sem ferramentas extremas: perde em alcance para especialistas em chute e em dano para pesados. Especiais são puníveis se defendidos.";
            f.StatReach = 3; f.StatSpeed = 3; f.StatPower = 3;
            f.MaxHealth = 1000;
            f.WalkSpeed = 2.6f; f.BackWalkSpeed = 2.0f;
            f.DashSpeed = 7.5f; f.DashTicks = 14; f.BackDashSpeed = 6.5f; f.BackDashTicks = 16;
            f.JumpVelocity = 9.6f; f.JumpForwardSpeed = 3.0f; f.Gravity = 30f;
            f.Guard = GuardStyle.Kickboxer;
            f.Body = new BodyShape { Height = 1f, Bulk = 1f, Limb = 1f, LegLength = 1f };
            f.IdleBounce = 1.7f;
            f.Palettes = new[]
            {
                Pal("Azul", Hex("#1E5BD8"), Hex("#F2F4F8"), Hex("#C98E6B"), Hex("#E8F0FF"), Hex("#2A1C14")),
                Pal("Laranja", Hex("#E2731B"), Hex("#1B1B1F"), Hex("#C98E6B"), Hex("#FFD7A8"), Hex("#2A1C14")),
            };

            f.Punch = new MoveSpec(id, MoveSlot.Punch, "Jab", "Soco")
                .Frames(4, 2, 8).Dmg(35, 15, 11, 6).Lv(AttackLevel.High).Hit(0.72f, 1.42f, 0.52f, 0.22f).Push(0.22f, 0.4f)
                .Energy(5).Cancel(MoveSlot.Kick, MoveSlot.SpecialForward, MoveSlot.SpecialNeutral).Look(AnimPose.Jab, SfxKind.LightHit)
                .Desc("Rápido e seguro. Começa combos.").Build();
            f.Kick = new MoveSpec(id, MoveSlot.Kick, "Chute médio", "Chute")
                .Frames(7, 3, 14).Dmg(60, 18, 13, 7).Lv(AttackLevel.Mid).Hit(0.85f, 1.0f, 0.72f, 0.3f).Push(0.3f, 0.55f)
                .Energy(7).Cancel(MoveSlot.SpecialForward, MoveSlot.SpecialNeutral).Look(AnimPose.BodyKick, SfxKind.LightHit).Build();
            f.Heavy = new MoveSpec(id, MoveSlot.Heavy, "Cruzado", "Forte")
                .Frames(10, 3, 20).Dmg(95, 22, 15, 9).Lv(AttackLevel.Mid).Hit(0.85f, 1.35f, 0.62f, 0.3f).Push(0.5f, 0.7f)
                .Energy(10).Moves(1.4f, 2, 11).Look(AnimPose.Straight, SfxKind.HeavyHit)
                .Desc("Avança levemente. Punível se defendido.").Build();
            f.CrouchPunch = new MoveSpec(id, MoveSlot.CrouchPunch, "Soco agachado", "↓ + Soco")
                .Frames(4, 2, 9).Dmg(30, 15, 10, 6).Lv(AttackLevel.Mid).Hit(0.68f, 0.78f, 0.5f, 0.22f).Push(0.2f, 0.4f)
                .Energy(4).Cancel(MoveSlot.Low, MoveSlot.SpecialNeutral).Look(AnimPose.CrouchJab, SfxKind.LightHit).Build();
            f.Low = new MoveSpec(id, MoveSlot.Low, "Chute baixo", "↓ + Chute")
                .Frames(7, 3, 16).Dmg(50, 17, 12, 7).Lv(AttackLevel.Low).Hit(0.86f, 0.2f, 0.76f, 0.26f).Push(0.3f, 0.5f)
                .Energy(6).Cancel(MoveSlot.SpecialForward).Look(AnimPose.LowKick, SfxKind.LightHit)
                .Desc("Exige defesa agachada.").Build();
            f.AntiAir = new MoveSpec(id, MoveSlot.AntiAir, "Uppercut", "↓ + Forte")
                .Frames(6, 4, 22).Dmg(85, 20, 14, 9).Lv(AttackLevel.Mid).Hit(0.5f, 1.72f, 0.6f, 0.75f).Push(0.3f, 0.5f)
                .Energy(9).Kd(KnockdownType.Launch, 1.5f, 7.5f).Inv(InvulnType.Air, 0, 9).Look(AnimPose.Uppercut, SfxKind.HeavyHit)
                .Desc("Antiaéreo: imune a ataques aéreos no início.").Build();
            f.Air = new MoveSpec(id, MoveSlot.Air, "Chute voador", "No ar: Soco/Chute/Forte")
                .Frames(5, 6, 8).Dmg(60, 18, 12, 7).Lv(AttackLevel.Overhead).Hit(0.6f, 0.45f, 0.62f, 0.45f).Push(0.25f, 0.45f)
                .Energy(6).Air(4).Look(AnimPose.AirKick, SfxKind.LightHit).Build();
            f.Grab = new MoveSpec(id, MoveSlot.Grab, "Clinch e joelhada", "Agarrão")
                .Frames(4, 2, 22).Dmg(110, 0, 0, 8).Throw(0.45f, false).Energy(10).Look(AnimPose.Grab, SfxKind.Grab).Build();
            f.SpecialNeutral = new MoveSpec(id, MoveSlot.SpecialNeutral, "Chute giratório", "Especial")
                .Frames(13, 3, 22).Dmg(110, 24, 16, 10).Lv(AttackLevel.Mid).Hit(0.95f, 1.25f, 0.82f, 0.36f).Push(0.6f, 0.8f)
                .Energy(10).Kd(KnockdownType.Knockdown, 3.5f, 5f).Bounce().Special().Look(AnimPose.SpinKick, SfxKind.HeavyHit)
                .Desc("Derruba. Elegível para rebote nas cordas. Punível se defendido.").Build();
            f.SpecialForward = new MoveSpec(id, MoveSlot.SpecialForward, "Avanço Desafiante", "Especial + Frente")
                .Frames(9, 5, 18).Dmg(90, 20, 14, 9).Lv(AttackLevel.Mid).Hit(0.72f, 1.3f, 0.56f, 0.32f).Push(0.5f, 1.0f)
                .Energy(9).Moves(6.2f, 3, 14).Cancel(MoveSlot.Signature).Special().Look(AnimPose.DashPunch, SfxKind.HeavyHit)
                .Desc("Avanço ofensivo curto. Pode ser cancelado no golpe de assinatura.").Build();
            f.Signature = new MoveSpec(id, MoveSlot.Signature, "Último Round", "Assinatura (energia cheia)")
                .Frames(8, 12, 30).Dmg(70, 20, 14, 8).Lv(AttackLevel.Mid).Hit(0.8f, 1.2f, 0.85f, 0.65f).Push(0.12f, 0.3f)
                .Energy(0).Multi(4, 3).Moves(3.5f, 0, 14).Kd(KnockdownType.Launch, 3f, 7f).Special()
                .Look(AnimPose.Signature, SfxKind.Signature).Desc("Sequência de 4 golpes. Consome a barra cheia; pode ser defendida.").Build();
            f.ForwardHeavy = null;

            f.Combos = new[]
            {
                Combo("Pressão Desafiante", "Soco → Chute → Especial + Frente",
                    "Cancele cada golpe assim que acertar. Funciona a curta distância.",
                    MoveSlot.Punch, MoveSlot.Kick, MoveSlot.SpecialForward),
                Combo("Rasteiro", "↓ + Soco → ↓ + Chute",
                    "Termina com golpe baixo; seguro para testar a defesa agachada.",
                    MoveSlot.CrouchPunch, MoveSlot.Low),
            };
            return f;
        }

        // ------------------------------------------------------------------ BRUTUS

        static FighterDefinition Brutus()
        {
            const string id = "brutus";
            var f = NewFighter(id, "Brutus");
            f.Epithet = "O Brigador";
            f.Style = "Briga pesada";
            f.Summary = "Golpes lentos e devastadores, agarrões fortes e muita vida. Vence trocas e castiga quem fica perto.";
            f.Weakness = "Lento para andar e iniciar golpes, corpo grande e recuperação longa: é punido ao errar e sofre contra pressão rápida.";
            f.StatReach = 3; f.StatSpeed = 1; f.StatPower = 5;
            f.MaxHealth = 1150;
            f.WalkSpeed = 2.0f; f.BackWalkSpeed = 1.6f;
            f.DashSpeed = 6.0f; f.DashTicks = 16; f.BackDashSpeed = 5.2f; f.BackDashTicks = 18; f.BackDashInvulnTicks = 3;
            f.JumpVelocity = 9.2f; f.JumpForwardSpeed = 2.6f; f.Gravity = 32f; f.PreJumpTicks = 4; f.LandingTicks = 5;
            f.PushHalfWidth = 0.4f;
            f.PushHeight = 1.82f;
            f.CrouchPushHeight = 1.2f;
            f.StandHurtboxes = new[] { new Box(0.05f, 0.91f, 0.78f, 1.82f), new Box(0.14f, 1.74f, 0.36f, 0.34f) };
            f.CrouchHurtboxes = new[] { new Box(0.05f, 0.6f, 0.86f, 1.2f) };
            f.AirHurtboxes = new[] { new Box(0f, 0.9f, 0.78f, 1.4f) };
            f.Guard = GuardStyle.Brawler;
            f.Body = new BodyShape { Height = 1.08f, Bulk = 1.38f, Limb = 1.3f, LegLength = 0.95f };
            f.IdleBounce = 0.9f;
            f.Palettes = new[]
            {
                Pal("Vermelho", Hex("#C0262D"), Hex("#2E2E33"), Hex("#A8714F"), Hex("#FF8A73"), Hex("#151515")),
                Pal("Oliva", Hex("#5E6B2E"), Hex("#D9C9A3"), Hex("#A8714F"), Hex("#E9F0B5"), Hex("#151515")),
            };

            f.Punch = new MoveSpec(id, MoveSlot.Punch, "Direto pesado", "Soco")
                .Frames(6, 3, 11).Dmg(50, 17, 12, 7).Lv(AttackLevel.High).Hit(0.82f, 1.48f, 0.56f, 0.26f).Push(0.22f, 0.45f)
                .Energy(6).Cancel(MoveSlot.Kick, MoveSlot.SpecialForward).Look(AnimPose.Straight, SfxKind.LightHit).Build();
            f.Kick = new MoveSpec(id, MoveSlot.Kick, "Pisão frontal", "Chute")
                .Frames(9, 3, 16).Dmg(75, 20, 14, 8).Lv(AttackLevel.Mid).Hit(0.92f, 0.92f, 0.72f, 0.36f).Push(0.3f, 0.6f)
                .Energy(8).Cancel(MoveSlot.SpecialForward, MoveSlot.SpecialNeutral).Look(AnimPose.Stomp, SfxKind.HeavyHit).Build();
            f.Heavy = new MoveSpec(id, MoveSlot.Heavy, "Gancho de ferro", "Forte")
                .Frames(13, 4, 24).Dmg(125, 24, 16, 11).Lv(AttackLevel.Mid).Hit(0.9f, 1.3f, 0.72f, 0.42f).Push(0.6f, 0.8f)
                .Energy(12).Kd(KnockdownType.Knockdown, 2.5f, 4.5f).Look(AnimPose.Hook, SfxKind.HeavyHit)
                .Desc("Derruba. Muito punível se defendido.").Build();
            f.ForwardHeavy = new MoveSpec(id, MoveSlot.ForwardHeavy, "Marretada", "Frente + Forte")
                .Frames(18, 3, 20).Dmg(90, 22, 14, 10).Lv(AttackLevel.Overhead).Hit(0.86f, 1.2f, 0.62f, 0.82f).Push(0.4f, 0.6f)
                .Energy(9).Look(AnimPose.Overhead, SfxKind.HeavyHit).Desc("Overhead: exige defesa em pé.").Build();
            f.CrouchPunch = new MoveSpec(id, MoveSlot.CrouchPunch, "Soco baixo", "↓ + Soco")
                .Frames(6, 3, 12).Dmg(45, 16, 11, 7).Lv(AttackLevel.Mid).Hit(0.74f, 0.8f, 0.56f, 0.26f).Push(0.2f, 0.4f)
                .Energy(5).Cancel(MoveSlot.Low).Look(AnimPose.CrouchJab, SfxKind.LightHit).Build();
            f.Low = new MoveSpec(id, MoveSlot.Low, "Rasteira bruta", "↓ + Chute")
                .Frames(11, 4, 24).Dmg(70, 0, 13, 8).Lv(AttackLevel.Low).Hit(0.92f, 0.2f, 0.84f, 0.3f).Push(0.3f, 0.5f)
                .Energy(8).Kd(KnockdownType.Sweep).Look(AnimPose.Sweep, SfxKind.HeavyHit).Desc("Derruba. Exige defesa agachada.").Build();
            f.AntiAir = new MoveSpec(id, MoveSlot.AntiAir, "Cabeçada ascendente", "↓ + Forte")
                .Frames(8, 5, 24).Dmg(100, 22, 15, 10).Lv(AttackLevel.Mid).Hit(0.45f, 1.85f, 0.7f, 0.7f).Push(0.3f, 0.5f)
                .Energy(10).Kd(KnockdownType.Launch, 1.5f, 7f).Inv(InvulnType.Air, 0, 11).Look(AnimPose.Headbutt, SfxKind.HeavyHit)
                .Desc("Antiaéreo lento, mas forte.").Build();
            f.Air = new MoveSpec(id, MoveSlot.Air, "Cotovelada aérea", "No ar: Soco/Chute/Forte")
                .Frames(7, 6, 10).Dmg(80, 19, 13, 8).Lv(AttackLevel.Overhead).Hit(0.55f, 0.6f, 0.62f, 0.55f).Push(0.25f, 0.45f)
                .Energy(7).Air(6).Look(AnimPose.AirPunch, SfxKind.HeavyHit).Build();
            f.Grab = new MoveSpec(id, MoveSlot.Grab, "Suplex", "Agarrão")
                .Frames(5, 3, 26).Dmg(165, 0, 0, 9).Throw(0.7f, true).Energy(12).Look(AnimPose.Grab, SfxKind.Grab)
                .Desc("Alcance maior que o normal. Arremessa para trás.").Build();
            f.SpecialNeutral = new MoveSpec(id, MoveSlot.SpecialNeutral, "Abraço de Urso", "Especial")
                .Frames(8, 3, 34).Dmg(190, 0, 0, 10).Throw(0.6f, false).Energy(14).Special().Look(AnimPose.Grab, SfxKind.Grab)
                .Desc("Agarrão especial de alto dano. Ao errar, recuperação enorme.").Build();
            f.SpecialForward = new MoveSpec(id, MoveSlot.SpecialForward, "Investida de ombro", "Especial + Frente")
                .Frames(14, 6, 24).Dmg(110, 24, 16, 11).Lv(AttackLevel.Mid).Hit(0.72f, 1.25f, 0.62f, 0.9f).Push(0.6f, 1.0f)
                .Energy(10).Moves(6.5f, 4, 20).Kd(KnockdownType.Knockdown, 4f, 4.5f).Bounce().Cancel(MoveSlot.Signature).Special()
                .Look(AnimPose.Shoulder, SfxKind.HeavyHit).Desc("Derruba. Elegível para rebote nas cordas.").Build();
            f.Signature = new MoveSpec(id, MoveSlot.Signature, "Terremoto", "Assinatura (energia cheia)")
                .Frames(10, 6, 40).Dmg(260, 24, 18, 14).Lv(AttackLevel.Mid).Hit(1.0f, 0.6f, 2.0f, 1.2f).Push(0.6f, 0.9f)
                .Energy(0).Kd(KnockdownType.Launch, 2f, 8f).Special().Look(AnimPose.Signature, SfxKind.Signature)
                .Desc("Golpe único de área curta. Muito punível se defendido.").Build();

            f.Combos = new[]
            {
                Combo("Rolo Compressor", "Soco → Chute → Especial + Frente",
                    "Termina com a investida (derruba; elegível para rebote).",
                    MoveSlot.Punch, MoveSlot.Kick, MoveSlot.SpecialForward),
                Combo("Varredura", "↓ + Soco → ↓ + Chute",
                    "Derruba com a rasteira.",
                    MoveSlot.CrouchPunch, MoveSlot.Low),
            };
            return f;
        }

        // ------------------------------------------------------------------ FAÍSCA

        static FighterDefinition Faisca()
        {
            const string id = "faisca";
            var f = NewFighter(id, "Faísca");
            f.Epithet = "A Relâmpago";
            f.Style = "Pressão ágil";
            f.Summary = "Golpes muito rápidos, avanço rasteiro e rajadas. Vive colada no adversário e sufoca com sequências curtas.";
            f.Weakness = "Pouca vida e dano baixo por golpe; alcance curto obriga a se aproximar, e ela é punida ao ser mantida à distância.";
            f.StatReach = 2; f.StatSpeed = 5; f.StatPower = 2;
            f.MaxHealth = 900;
            f.WalkSpeed = 3.2f; f.BackWalkSpeed = 2.6f;
            f.DashSpeed = 9.0f; f.DashTicks = 12; f.BackDashSpeed = 7.5f; f.BackDashTicks = 14; f.BackDashInvulnTicks = 6;
            f.JumpVelocity = 10.4f; f.JumpForwardSpeed = 3.6f; f.Gravity = 33f; f.PreJumpTicks = 2; f.LandingTicks = 2;
            f.PushHalfWidth = 0.28f;
            f.PushHeight = 1.62f;
            f.CrouchPushHeight = 1.0f;
            f.StandHurtboxes = new[] { new Box(0.04f, 0.81f, 0.54f, 1.62f), new Box(0.1f, 1.54f, 0.3f, 0.3f) };
            f.CrouchHurtboxes = new[] { new Box(0.04f, 0.5f, 0.62f, 1.0f) };
            f.AirHurtboxes = new[] { new Box(0f, 0.8f, 0.54f, 1.2f) };
            f.Guard = GuardStyle.Agile;
            f.Body = new BodyShape { Height = 0.95f, Bulk = 0.85f, Limb = 0.85f, LegLength = 1.03f };
            f.IdleBounce = 2.4f;
            f.Palettes = new[]
            {
                Pal("Amarelo", Hex("#F2C318"), Hex("#151517"), Hex("#E0A985"), Hex("#FFF2A8"), Hex("#3A1E10")),
                Pal("Ciano", Hex("#18C6D9"), Hex("#F4F6FA"), Hex("#E0A985"), Hex("#C8FAFF"), Hex("#3A1E10")),
            };

            f.Punch = new MoveSpec(id, MoveSlot.Punch, "Jab relâmpago", "Soco")
                .Frames(3, 2, 7).Dmg(25, 14, 10, 5).Lv(AttackLevel.High).Hit(0.64f, 1.36f, 0.46f, 0.22f).Push(0.18f, 0.35f)
                .Energy(5).Cancel(MoveSlot.Kick, MoveSlot.SpecialNeutral, MoveSlot.SpecialForward).Look(AnimPose.Jab, SfxKind.LightHit).Build();
            f.Kick = new MoveSpec(id, MoveSlot.Kick, "Chute rápido", "Chute")
                .Frames(5, 3, 10).Dmg(40, 16, 11, 6).Lv(AttackLevel.Mid).Hit(0.74f, 1.0f, 0.56f, 0.3f).Push(0.22f, 0.45f)
                .Energy(6).Cancel(MoveSlot.SpecialNeutral, MoveSlot.SpecialForward).Look(AnimPose.BodyKick, SfxKind.LightHit).Build();
            f.Heavy = new MoveSpec(id, MoveSlot.Heavy, "Cotovelada giratória", "Forte")
                .Frames(8, 3, 17).Dmg(70, 20, 13, 8).Lv(AttackLevel.Mid).Hit(0.66f, 1.3f, 0.5f, 0.32f).Push(0.4f, 0.6f)
                .Energy(8).Moves(1.6f, 1, 9).Cancel(MoveSlot.SpecialForward).Look(AnimPose.Hook, SfxKind.HeavyHit)
                .Desc("Avança um pouco. Punível se defendido.").Build();
            f.CrouchPunch = new MoveSpec(id, MoveSlot.CrouchPunch, "Soco rasteiro", "↓ + Soco")
                .Frames(3, 2, 8).Dmg(22, 14, 10, 5).Lv(AttackLevel.Mid).Hit(0.62f, 0.76f, 0.46f, 0.22f).Push(0.15f, 0.3f)
                .Energy(4).Cancel(MoveSlot.Low, MoveSlot.SpecialForward).Look(AnimPose.CrouchJab, SfxKind.LightHit).Build();
            f.Low = new MoveSpec(id, MoveSlot.Low, "Rasteira veloz", "↓ + Chute")
                .Frames(6, 3, 14).Dmg(40, 16, 11, 6).Lv(AttackLevel.Low).Hit(0.78f, 0.2f, 0.66f, 0.26f).Push(0.22f, 0.4f)
                .Energy(5).Cancel(MoveSlot.SpecialForward).Look(AnimPose.LowKick, SfxKind.LightHit).Desc("Exige defesa agachada.").Build();
            f.AntiAir = new MoveSpec(id, MoveSlot.AntiAir, "Mortal ascendente", "↓ + Forte")
                .Frames(5, 5, 20).Dmg(70, 20, 13, 8).Lv(AttackLevel.Mid).Hit(0.42f, 1.7f, 0.66f, 0.8f).Push(0.3f, 0.5f)
                .Energy(8).Kd(KnockdownType.Launch, 1.2f, 7f).Inv(InvulnType.Air, 0, 8).Look(AnimPose.HighKick, SfxKind.HeavyHit)
                .Desc("Antiaéreo rápido.").Build();
            f.Air = new MoveSpec(id, MoveSlot.Air, "Joelhada aérea", "No ar: Soco/Chute/Forte")
                .Frames(4, 6, 6).Dmg(45, 16, 11, 6).Lv(AttackLevel.Overhead).Hit(0.52f, 0.55f, 0.56f, 0.45f).Push(0.2f, 0.4f)
                .Energy(5).Air(3).Look(AnimPose.AirKick, SfxKind.LightHit).Build();
            f.Grab = new MoveSpec(id, MoveSlot.Grab, "Chave de braço", "Agarrão")
                .Frames(4, 2, 20).Dmg(100, 0, 0, 8).Throw(0.4f, true).Energy(10).Look(AnimPose.Grab, SfxKind.Grab)
                .Desc("Troca de lado com o adversário.").Build();
            f.SpecialNeutral = new MoveSpec(id, MoveSlot.SpecialNeutral, "Rajada de socos", "Especial")
                .Frames(6, 12, 16).Dmg(22, 16, 11, 4).Lv(AttackLevel.Mid).Hit(0.66f, 1.25f, 0.6f, 0.42f).Push(0.08f, 0.15f)
                .Energy(3).Multi(4, 3).Cancel(MoveSlot.Signature).Special().Look(AnimPose.Flurry, SfxKind.LightHit)
                .Desc("Quatro socos rápidos (hits explícitos). Pode ser cancelada na assinatura.").Build();
            f.SpecialForward = new MoveSpec(id, MoveSlot.SpecialForward, "Faísca rasante", "Especial + Frente")
                .Frames(8, 8, 18).Dmg(65, 0, 13, 8).Lv(AttackLevel.Low).Hit(0.6f, 0.25f, 0.7f, 0.4f).Push(0.4f, 0.7f)
                .Energy(8).Moves(7.5f, 2, 16).Kd(KnockdownType.Sweep).Special().Look(AnimPose.Sweep, SfxKind.HeavyHit)
                .Desc("Deslize rasteiro que derruba. Golpe baixo; punível se defendido.").Build();
            f.Signature = new MoveSpec(id, MoveSlot.Signature, "Curto-circuito", "Assinatura (energia cheia)")
                .Frames(6, 18, 30).Dmg(45, 18, 12, 6).Lv(AttackLevel.Mid).Hit(0.75f, 1.15f, 0.8f, 0.7f).Push(0.06f, 0.2f)
                .Energy(0).Multi(6, 3).Moves(5f, 0, 12).Kd(KnockdownType.Launch, 2.5f, 7f).Special()
                .Look(AnimPose.Signature, SfxKind.Signature).Desc("Seis golpes elétricos em sequência.").Build();
            f.ForwardHeavy = null;

            f.Combos = new[]
            {
                Combo("Choque em cadeia", "Soco → Chute → Especial",
                    "Termina com a rajada de 4 socos.",
                    MoveSlot.Punch, MoveSlot.Kick, MoveSlot.SpecialNeutral),
                Combo("Curto e rasteiro", "↓ + Soco → ↓ + Chute → Especial + Frente",
                    "Dois golpes baixos e o deslize que derruba.",
                    MoveSlot.CrouchPunch, MoveSlot.Low, MoveSlot.SpecialForward),
            };
            return f;
        }

        // ------------------------------------------------------------------ VÉRTICE

        static FighterDefinition Vertice()
        {
            const string id = "vertice";
            var f = NewFighter(id, "Vértice");
            f.Epithet = "A Lâmina";
            f.Style = "Especialista em chutes";
            f.Summary = "Chutes longos que controlam a distância, overhead de machado e um leque que alcança quase meio ringue.";
            f.Weakness = "Socos lentos e fracos de perto; chutes longos têm recuperação grande e o membro estendido pode ser punido ao errar.";
            f.StatReach = 5; f.StatSpeed = 3; f.StatPower = 3;
            f.MaxHealth = 980;
            f.WalkSpeed = 2.4f; f.BackWalkSpeed = 2.2f;
            f.DashSpeed = 7.0f; f.DashTicks = 14; f.BackDashSpeed = 6.8f; f.BackDashTicks = 16;
            f.JumpVelocity = 9.8f; f.JumpForwardSpeed = 3.0f; f.Gravity = 30f;
            f.PushHalfWidth = 0.31f;
            f.PushHeight = 1.76f;
            f.StandHurtboxes = new[] { new Box(0.04f, 0.88f, 0.58f, 1.76f), new Box(0.12f, 1.66f, 0.3f, 0.32f) };
            f.Guard = GuardStyle.KickSpecialist;
            f.Body = new BodyShape { Height = 1.04f, Bulk = 0.92f, Limb = 0.9f, LegLength = 1.12f };
            f.IdleBounce = 1.2f;
            f.Palettes = new[]
            {
                Pal("Roxo", Hex("#6A2BC2"), Hex("#C9CDD6"), Hex("#B97E5C"), Hex("#E2D4FF"), Hex("#D8D8E0")),
                Pal("Esmeralda", Hex("#138A5A"), Hex("#D9B44A"), Hex("#B97E5C"), Hex("#C8FFE6"), Hex("#2B1A12")),
            };

            f.Punch = new MoveSpec(id, MoveSlot.Punch, "Jab de contenção", "Soco")
                .Frames(5, 2, 10).Dmg(30, 14, 10, 6).Lv(AttackLevel.High).Hit(0.68f, 1.42f, 0.46f, 0.22f).Push(0.2f, 0.4f)
                .Energy(4).Cancel(MoveSlot.Kick).Look(AnimPose.Jab, SfxKind.LightHit).Build();
            f.Kick = new MoveSpec(id, MoveSlot.Kick, "Chute frontal longo", "Chute")
                .Frames(8, 3, 16).Dmg(65, 19, 13, 7).Lv(AttackLevel.Mid).Hit(1.05f, 1.05f, 0.92f, 0.3f).Push(0.32f, 0.6f)
                .Energy(7).Cancel(MoveSlot.SpecialNeutral, MoveSlot.SpecialForward).Look(AnimPose.BodyKick, SfxKind.LightHit)
                .Desc("Alcance longo.").Build();
            f.Heavy = new MoveSpec(id, MoveSlot.Heavy, "Chute alto giratório", "Forte")
                .Frames(12, 3, 24).Dmg(105, 22, 15, 10).Lv(AttackLevel.High).Hit(1.1f, 1.45f, 0.92f, 0.36f).Push(0.55f, 0.75f)
                .Energy(10).Kd(KnockdownType.Knockdown, 2.5f, 5f).Bounce().Look(AnimPose.HighKick, SfxKind.HeavyHit)
                .Desc("Alto: passa por cima de quem está agachado. Derruba; elegível para rebote.").Build();
            f.ForwardHeavy = new MoveSpec(id, MoveSlot.ForwardHeavy, "Machado", "Frente + Forte")
                .Frames(20, 3, 22).Dmg(95, 22, 14, 10).Lv(AttackLevel.Overhead).Hit(0.95f, 1.25f, 0.62f, 0.85f).Push(0.4f, 0.6f)
                .Energy(9).Look(AnimPose.Axe, SfxKind.HeavyHit).Desc("Overhead: exige defesa em pé.").Build();
            f.CrouchPunch = new MoveSpec(id, MoveSlot.CrouchPunch, "Chute agachado curto", "↓ + Soco")
                .Frames(5, 2, 10).Dmg(30, 16, 11, 6).Lv(AttackLevel.Mid).Hit(0.76f, 0.7f, 0.6f, 0.24f).Push(0.18f, 0.35f)
                .Energy(4).Cancel(MoveSlot.Low).Look(AnimPose.CrouchJab, SfxKind.LightHit).Build();
            f.Low = new MoveSpec(id, MoveSlot.Low, "Chute rasteiro longo", "↓ + Chute")
                .Frames(9, 3, 20).Dmg(55, 18, 12, 7).Lv(AttackLevel.Low).Hit(1.08f, 0.2f, 1.0f, 0.26f).Push(0.3f, 0.5f)
                .Energy(6).Cancel(MoveSlot.SpecialForward).Look(AnimPose.LowKick, SfxKind.LightHit).Desc("Baixo de longo alcance.").Build();
            f.AntiAir = new MoveSpec(id, MoveSlot.AntiAir, "Chute meia-lua", "↓ + Forte")
                .Frames(7, 5, 24).Dmg(85, 20, 14, 9).Lv(AttackLevel.Mid).Hit(0.62f, 1.82f, 0.92f, 0.8f).Push(0.3f, 0.5f)
                .Energy(9).Kd(KnockdownType.Launch, 1.5f, 7.5f).Inv(InvulnType.Air, 0, 10).Look(AnimPose.HighKick, SfxKind.HeavyHit)
                .Desc("Antiaéreo de grande arco.").Build();
            f.Air = new MoveSpec(id, MoveSlot.Air, "Voadora", "No ar: Soco/Chute/Forte")
                .Frames(6, 7, 8).Dmg(65, 18, 12, 7).Lv(AttackLevel.Overhead).Hit(0.76f, 0.5f, 0.74f, 0.36f).Push(0.25f, 0.45f)
                .Energy(6).Air(4).Look(AnimPose.AirKick, SfxKind.LightHit).Build();
            f.Grab = new MoveSpec(id, MoveSlot.Grab, "Tesoura", "Agarrão")
                .Frames(5, 2, 22).Dmg(105, 0, 0, 8).Throw(0.4f, true).Energy(10).Look(AnimPose.Grab, SfxKind.Grab)
                .Desc("Troca de lado com o adversário.").Build();
            f.SpecialNeutral = new MoveSpec(id, MoveSlot.SpecialNeutral, "Leque", "Especial")
                .Frames(14, 4, 26).Dmg(100, 22, 15, 10).Lv(AttackLevel.Mid).Hit(1.35f, 1.15f, 1.1f, 0.42f).Push(0.5f, 1.0f)
                .Energy(10).Kd(KnockdownType.Knockdown, 3f, 5f).Bounce().Special().Look(AnimPose.SpinKick, SfxKind.HeavyHit)
                .Desc("Alcance enorme. Derruba; elegível para rebote. Muito punível se defendido.").Build();
            f.SpecialForward = new MoveSpec(id, MoveSlot.SpecialForward, "Passo cortante", "Especial + Frente")
                .Frames(11, 4, 20).Dmg(80, 20, 14, 9).Lv(AttackLevel.Mid).Hit(0.95f, 1.1f, 0.82f, 0.36f).Push(0.4f, 0.9f)
                .Energy(9).Moves(5f, 2, 12).Cancel(MoveSlot.Signature).Special().Look(AnimPose.DashPunch, SfxKind.HeavyHit)
                .Desc("Avança e chuta. Pode ser cancelado na assinatura.").Build();
            f.Signature = new MoveSpec(id, MoveSlot.Signature, "Vórtice", "Assinatura (energia cheia)")
                .Frames(9, 15, 34).Dmg(90, 20, 14, 9).Lv(AttackLevel.Mid).Hit(1.1f, 1.2f, 1.1f, 0.85f).Push(0.1f, 0.3f)
                .Energy(0).Multi(3, 5).Moves(2.5f, 0, 12).Kd(KnockdownType.Launch, 3f, 7.5f).Special()
                .Look(AnimPose.Signature, SfxKind.Signature).Desc("Três chutes giratórios de longo alcance.").Build();

            f.Combos = new[]
            {
                Combo("Corte de distância", "↓ + Soco → ↓ + Chute → Especial + Frente",
                    "Começa baixo e termina avançando.",
                    MoveSlot.CrouchPunch, MoveSlot.Low, MoveSlot.SpecialForward),
                Combo("Leque final", "Soco → Chute → Especial",
                    "Termina com o Leque (derruba; rebote perto das cordas).",
                    MoveSlot.Punch, MoveSlot.Kick, MoveSlot.SpecialNeutral),
            };
            return f;
        }
    }
}
