using GTANetworkAPI;
using static Utility.Enumerators;

namespace Data.Temporary
{
    public class GunModel
    {
        public WeaponHash Weapon { get; set; }
        public string Ammunition { get; set; }
        public int Capacity { get; set; }
        public WeaponTypes WeaponType { get; set; }


        public GunModel(WeaponHash weapon, WeaponTypes type, string ammunition, int capacity)
        {
            Weapon = weapon;
            WeaponType = type;
            Ammunition = ammunition;
            Capacity = capacity;
        }
    }
}
