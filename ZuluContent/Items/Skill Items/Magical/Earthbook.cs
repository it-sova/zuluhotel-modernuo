using Server.Gumps;
using Server.Spells;

namespace Server.Items
{
    [CustomSpellSchool(CustomSpellSchoolType.Earth)]
    public class Earthbook : CustomSpellbook
    {
        [Constructible]
        public Earthbook() : base(0x0EFA)
        {
            Name = "Book of the Earth";
            Hue = 0x48A;
        }

        [Constructible]
        public Earthbook(Serial serial) : base(serial)
        {
        }

        public override void OnOpenSpellbook(Mobile from)
        {
            from.SendGump(new EarthbookGump(from, this));
        }

        public override void Serialize(IGenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write(0);
        }

        public override void Deserialize(IGenericReader reader)
        {
            base.Deserialize(reader);

            var version = reader.ReadInt();
        }
    }
}