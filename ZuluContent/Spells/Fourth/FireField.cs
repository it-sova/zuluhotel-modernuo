using System;
using System.Collections;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Spells.Fourth
{
    public class FireFieldSpell : MagerySpell
    {
        public FireFieldSpell(Mobile caster, Item scroll) : base(caster, scroll)
        {
        }


        public override void OnCast()
        {
            Caster.Target = new InternalTarget(this);
        }

        public void Target(IPoint3D p)
        {
            if (!Caster.CanSee(p))
            {
                Caster.SendLocalizedMessage(500237); // Target can not be seen.
            }
            else if (SpellHelper.CheckTown(p, Caster) && CheckSequence())
            {
                SpellHelper.Turn(Caster, p);

                SpellHelper.GetSurfaceTop(ref p);

                var dx = Caster.Location.X - p.X;
                var dy = Caster.Location.Y - p.Y;
                var rx = (dx - dy) * 44;
                var ry = (dx + dy) * 44;

                bool eastToWest;

                if (rx >= 0 && ry >= 0)
                    eastToWest = false;
                else if (rx >= 0)
                    eastToWest = true;
                else if (ry >= 0)
                    eastToWest = true;
                else
                    eastToWest = false;

                Effects.PlaySound((Point3D)p, Caster.Map, 0x20C);

                var itemID = eastToWest ? 0x398C : 0x3996;

                var duration = TimeSpan.FromSeconds(4.0 + Caster.Skills[SkillName.Magery].Value * 0.5);

                for (var i = -2; i <= 2; ++i)
                {
                    var loc = new Point3D(eastToWest ? p.X + i : p.X, eastToWest ? p.Y : p.Y + i, p.Z);

                    new FireFieldItem(itemID, loc, Caster, Caster.Map, duration, i);
                }
            }

            FinishSequence();
        }

        [DispellableField]
        public class FireFieldItem : Item
        {
            private Mobile m_Caster;
            private int m_Damage;
            private DateTime m_End;
            private Timer m_Timer;

            public FireFieldItem(int itemID, Point3D loc, Mobile caster, Map map, TimeSpan duration, int val)
                : this(itemID, loc, caster, map, duration, val, 2)
            {
            }

            public FireFieldItem(int itemID, Point3D loc, Mobile caster, Map map, TimeSpan duration, int val,
                int damage) : base(itemID)
            {
                var canFit = SpellHelper.AdjustField(ref loc, map, 12, false);

                Visible = false;
                Movable = false;
                Light = LightType.Circle300;

                MoveToWorld(loc, map);

                m_Caster = caster;

                m_Damage = damage;

                m_End = DateTime.Now + duration;

                m_Timer = new InternalTimer(this, TimeSpan.FromSeconds(Math.Abs(val) * 0.2), caster.InLOS(this),
                    canFit);
                m_Timer.Start();
            }

            public FireFieldItem(Serial serial) : base(serial)
            {
            }

            public override bool BlocksFit
            {
                get { return true; }
            }

            public override void OnAfterDelete()
            {
                base.OnAfterDelete();

                if (m_Timer != null)
                    m_Timer.Stop();
            }

            public override void Serialize(IGenericWriter writer)
            {
                base.Serialize(writer);

                writer.Write(2); // version

                writer.Write(m_Damage);
                writer.Write(m_Caster);
                writer.WriteDeltaTime(m_End);
            }

            public override void Deserialize(IGenericReader reader)
            {
                base.Deserialize(reader);

                var version = reader.ReadInt();

                switch (version)
                {
                    case 2:
                    {
                        m_Damage = reader.ReadInt();
                        goto case 1;
                    }
                    case 1:
                    {
                        m_Caster = reader.ReadEntity<Mobile>();

                        goto case 0;
                    }
                    case 0:
                    {
                        m_End = reader.ReadDeltaTime();

                        m_Timer = new InternalTimer(this, TimeSpan.Zero, true, true);
                        m_Timer.Start();

                        break;
                    }
                }

                if (version < 2)
                    m_Damage = 2;
            }

            public override bool OnMoveOver(Mobile m)
            {
                if (Visible && m_Caster != null && SpellHelper.ValidIndirectTarget(m_Caster, m) &&
                    m_Caster.CanBeHarmful(m, false))
                {
                    if (SpellHelper.CanRevealCaster(m))
                        m_Caster.RevealingAction();

                    m_Caster.DoHarmful(m);

                    var damage = m_Damage;

                    if (m.CheckSkill(SkillName.MagicResist, 0.0, 30.0))
                    {
                        damage = 1;

                        m.SendLocalizedMessage(501783); // You feel yourself resisting magical energy.
                    }

                    m.Damage(damage, m_Caster);
                    m.PlaySound(0x208);

                    if (m is BaseCreature)
                        ((BaseCreature) m).OnHarmfulSpell(m_Caster);
                }

                return true;
            }

            private class InternalTimer : Timer
            {
                private static readonly Queue m_Queue = new Queue();
                private readonly bool m_InLOS;
                private readonly bool m_CanFit;
                private readonly FireFieldItem m_Item;

                public InternalTimer(FireFieldItem item, TimeSpan delay, bool inLOS, bool canFit) : base(delay,
                    TimeSpan.FromSeconds(1.0))
                {
                    m_Item = item;
                    m_InLOS = inLOS;
                    m_CanFit = canFit;

                    Priority = TimerPriority.FiftyMS;
                }

                protected override void OnTick()
                {
                    if (m_Item.Deleted)
                        return;

                    if (!m_Item.Visible)
                    {
                        if (m_InLOS && m_CanFit)
                            m_Item.Visible = true;
                        else
                            m_Item.Delete();

                        if (!m_Item.Deleted)
                        {
                            m_Item.ProcessDelta();
                            Effects.SendLocationParticles(
                                EffectItem.Create(m_Item.Location, m_Item.Map, EffectItem.DefaultDuration), 0x376A, 9,
                                10, 5029);
                        }
                    }
                    else if (DateTime.Now > m_Item.m_End)
                    {
                        m_Item.Delete();
                        Stop();
                    }
                    else
                    {
                        var map = m_Item.Map;
                        var caster = m_Item.m_Caster;

                        if (map != null && caster != null)
                        {
                            foreach (var m in m_Item.GetMobilesInRange(0))
                                if (m.Z + 16 > m_Item.Z && m_Item.Z + 12 > m.Z && m != caster &&
                                    SpellHelper.ValidIndirectTarget(caster, m) && caster.CanBeHarmful(m, false))
                                    m_Queue.Enqueue(m);

                            while (m_Queue.Count > 0)
                            {
                                var m = (Mobile) m_Queue.Dequeue();

                                if (SpellHelper.CanRevealCaster(m))
                                    caster.RevealingAction();

                                caster.DoHarmful(m);

                                var damage = m_Item.m_Damage;

                                if (m.CheckSkill(SkillName.MagicResist, 0.0, 30.0))
                                {
                                    damage = 1;

                                    m.SendLocalizedMessage(501783); // You feel yourself resisting magical energy.
                                }

                                m.Damage(damage, caster);
                                m.PlaySound(0x208);

                                if (m is BaseCreature)
                                    ((BaseCreature) m).OnHarmfulSpell(caster);
                            }
                        }
                    }
                }
            }
        }

        private class InternalTarget : Target
        {
            private readonly FireFieldSpell m_Owner;

            public InternalTarget(FireFieldSpell owner) : base(12, true, TargetFlags.None)
            {
                m_Owner = owner;
            }

            protected override void OnTarget(Mobile from, object o)
            {
                if (o is IPoint3D)
                    m_Owner.Target((IPoint3D) o);
            }

            protected override void OnTargetFinish(Mobile from)
            {
                m_Owner.FinishSequence();
            }
        }
    }
}