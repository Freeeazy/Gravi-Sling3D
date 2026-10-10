using System;
using UnityEngine;

public struct QuestFlavor
{
    public string questTitle;
    public string shortDescription;
    public string fullDescription;
    public string deliveryItemName;

    public CargoEffectType cargoEffectType;

    // Faction responsible for this delivery.
    public NPCFaction sourceFaction;

    // Shared visual color.
    public Color DeliveryItemColor => QuestColorUtil.GetCargoColor(cargoEffectType, sourceFaction);

    // Colored package name for TextMeshPro.
    public string ColoredItemName => QuestColorUtil.Colorize(deliveryItemName, DeliveryItemColor);

    // Description with package name highlighted.
    public string ColoredFullDescription => QuestColorUtil.ColorizeDescription(fullDescription, deliveryItemName, DeliveryItemColor);
    public string ColoredShortDescription => QuestColorUtil.ColorizeDescription(shortDescription, deliveryItemName, DeliveryItemColor);
}

public static class QuestFlavorUtil
{
    public static QuestFlavor Generate(
    NPCData npc,
    NPCQuestManager.QuestOffer offer,
    string destinationName,
    int seed)
    {
        QuestFlavor flavor = GenerateRaw(
            npc,
            offer,
            destinationName,
            seed
        );

        NPCProfile profile =
            npc.profile.npcId == npc.npcId
                ? npc.profile
                : NPCProfileUtil.GenerateProfile(npc.npcId);

        flavor.sourceFaction = profile.primaryFaction;

        return flavor;
    }
    public static QuestFlavor GenerateRaw(
        NPCData npc,
        NPCQuestManager.QuestOffer offer,
        string destinationName,
        int seed)
    {
        var rng = new System.Random(seed ^ npc.npcId);

        NPCProfile profile =
            npc.profile.npcId == npc.npcId
                ? npc.profile
                : NPCProfileUtil.GenerateProfile(npc.npcId);

        string npcName =
            string.IsNullOrWhiteSpace(npc.displayName)
                ? "This client"
                : npc.displayName;

        // -------------------------------------------------
        // PERSONALITY COMBINATIONS
        // -------------------------------------------------

        if (Has(profile, NPCPersonalityTrait.Family) &&
            (profile.ageBand == NPCAgeBand.Older ||
             profile.ageBand == NPCAgeBand.Elder ||
             profile.primaryFaction == NPCFaction.Friendly))
        {
            int familyRoll = rng.Next(0, 3);

            if (familyRoll == 0)
            {
                return Make(
                    "Grandkid Supply Run",
                    "New Mag-Boots",
                    $"{npcName} says their grandkid wore through another pair of station shoes.",
                    $"Deliver the New Mag-Boots to {destinationName}. {npcName} insists the kid is \"one bad puddle away from walking in taped-up scrap.\""
                );
            }

            if (familyRoll == 1)
            {
                return Make(
                    "Dinner's Getting Cold",
                    "Homemade Dinner",
                    $"{npcName} packed dinner for someone who forgot to eat again.",
                    $"Deliver the Homemade Dinner to {destinationName}. {npcName} says reheating it twice is acceptable. Three times is apparently a personal insult."
                );
            }

            return Make(
                "Something From Home",
                "Family Photo Box",
                $"{npcName} has a small box of old family keepsakes that needs delivering.",
                $"Deliver the Family Photo Box to {destinationName}. {npcName} asks you not to lose it. There are things in there that cannot be replaced."
            );
        }

        if (Has(profile, NPCPersonalityTrait.Petty) &&
            Has(profile, NPCPersonalityTrait.Romantic))
        {
            int pettyRomanceRoll = rng.Next(0, 3);

            if (pettyRomanceRoll == 0)
            {
                return Make(
                    "Absolutely Normal Chocolates",
                    "Suspicious Chocolates",
                    $"{npcName} has apology chocolates for an ex. The grin is worrying.",
                    $"Deliver the Suspicious Chocolates to {destinationName}. {npcName} refuses to explain why the box has medical warnings."
                );
            }

            if (pettyRomanceRoll == 1)
            {
                return Make(
                    "Closure, Apparently",
                    "Ex's Belongings",
                    $"{npcName} wants the last of an ex's belongings out of their station.",
                    $"Deliver the Ex's Belongings to {destinationName}. {npcName} says everything is accounted for except \"several years of wasted time.\""
                );
            }

            return Make(
                "One Last Gift",
                "Wilted Flowers",
                $"{npcName} insists these flowers still make a statement.",
                $"Deliver the Wilted Flowers to {destinationName}. Whether the statement is romantic or threatening is left deliberately unclear."
            );
        }

        if (Has(profile, NPCPersonalityTrait.Petty))
        {
            int pettyRoll = rng.Next(0, 3);

            if (pettyRoll == 0)
            {
                return Make(
                    "Return to Sender",
                    "Unwashed Coffee Mug",
                    $"{npcName} wants a borrowed mug returned with ceremonial levels of spite.",
                    $"Deliver the Unwashed Coffee Mug to {destinationName}. {npcName} specifically requests that you do not clean it."
                );
            }

            if (pettyRoll == 1)
            {
                return Make(
                    "Prove a Point",
                    "Single Left Boot",
                    $"{npcName} needs one boot delivered. There is apparently history here.",
                    $"Deliver the Single Left Boot to {destinationName}. {npcName} says the recipient will understand immediately."
                );
            }

            return Make(
                "Administrative Warfare",
                "Complaint Packet",
                $"{npcName} has compiled an aggressively thorough formal complaint.",
                $"Deliver the Complaint Packet to {destinationName}. It is 184 pages long and concerns a parking dispute."
            );
        }

        if (Has(profile, NPCPersonalityTrait.Romantic))
        {
            int romanticRoll = rng.Next(0, 3);

            if (romanticRoll == 0)
            {
                return Make(
                    "Across the Stars",
                    "Bouquet of Star-Lilies",
                    $"{npcName} has flowers for someone several stations away.",
                    $"Deliver the Bouquet of Star-Lilies to {destinationName}. {npcName} spent more on the flowers than your delivery fee."
                );
            }

            if (romanticRoll == 1)
            {
                return Make(
                    "Do Not Read This",
                    "Sealed Love Letter",
                    $"{npcName} nervously hands you a letter sealed three separate times.",
                    $"Deliver the Sealed Love Letter to {destinationName}. {npcName} will somehow know if you opened it."
                );
            }

            return Make(
                "Anniversary Emergency",
                "Anniversary Gift",
                $"{npcName} forgot an important date and is now outsourcing panic.",
                $"Deliver the Anniversary Gift to {destinationName}. Speed has suddenly become extremely important."
            );
        }

        // -------------------------------------------------
        // WEIRD CARGO
        // -------------------------------------------------

        if (Has(profile, NPCPersonalityTrait.Weird))
        {
            int weirdRoll = rng.Next(0, 3);

            if (weirdRoll == 0)
            {
                return Make(
                    "Lighter Than Nothing",
                    "Null-Gravity Core",
                    $"{npcName} hands you a Null-Gravity Core that barely seems to exist.",
                    $"Deliver the Null-Gravity Core to {destinationName}. Its mass-canceling field may make your ship unusually responsive.",
                    CargoEffectType.UltraLight
                );
            }

            if (weirdRoll == 1)
            {
                return Make(
                    "Deadweight Delivery",
                    "Artificial Gravity Core",
                    $"{npcName} hands you an Artificial Gravity Core dense enough to upset your ship.",
                    $"Deliver the Artificial Gravity Core to {destinationName}. Its gravity field adds considerable effective mass.",
                    CargoEffectType.SuperHeavy
                );
            }

            return Make(
                "Runaway Reactor",
                "Unstable Fusion Cell",
                $"{npcName} hands you an Unstable Fusion Cell that's practically overflowing with energy.",
                $"Deliver the Unstable Fusion Cell to {destinationName}. Its excess energy continuously overcharges your ship's boost systems, whether you like it or not.",
                CargoEffectType.OverchargedBoost
            );
        }

        // -------------------------------------------------
        // MECHANIC
        // -------------------------------------------------

        if (profile.primaryFaction == NPCFaction.Mechanic)
        {
            int mechanicRoll = rng.Next(0, 7);

            if (mechanicRoll == 0)
            {
                return Make(
                    "Emergency Coupler Run",
                    "Engine Couplers",
                    $"{npcName} needs repair parts moved before anyone notices the smoke.",
                    $"Deliver the Engine Couplers to {destinationName}. {npcName} claims everything is under control."
                );
            }

            if (mechanicRoll == 1)
            {
                return Make(
                    "Patch Job",
                    "Hull Repair Kit",
                    $"{npcName} needs a repair kit delivered to a ship with more hole than hull.",
                    $"Deliver the Hull Repair Kit to {destinationName}. Apparently duct tape has finally reached its limit."
                );
            }

            if (mechanicRoll == 2)
            {
                return Make(
                    "Wrong Part, Right Now",
                    "Replacement Thruster",
                    $"{npcName} found the correct thruster about three hours too late.",
                    $"Deliver the Replacement Thruster to {destinationName}. The recipient is currently operating on three."
                );
            }

            if (mechanicRoll == 3)
            {
                return Make(
                    "Definitely Refurbished",
                    "Used Reactor Pump",
                    $"{npcName} has a replacement reactor pump with only minor cosmetic damage.",
                    $"Deliver the Used Reactor Pump to {destinationName}. {npcName} asks that you ignore the scorch marks."
                );
            }

            if (mechanicRoll == 4)
            {
                return Make(
                    "Heavy Hauling",
                    "Capital Ship Engine",
                    $"{npcName} needs a massive ship engine transported. Your cargo hold groans at the thought.",
                    $"Deliver the Capital Ship Engine to {destinationName}. Its immense weight will significantly reduce your ship's maneuverability.",
                    CargoEffectType.SuperHeavy
                );
            }

            if (mechanicRoll == 5)
            {
                return Make(
                    "Faulty Power Coupler",
                    "Overloaded Power Coupler",
                    $"{npcName} hands you a power coupler that's still crackling with excess energy.",
                    $"Deliver the Overloaded Power Coupler to {destinationName}. Its unstable discharge is feeding directly into your ship's boost systems.",
                    CargoEffectType.OverchargedBoost
                );
            }

            return Make(
                "Bag of Bolts",
                "Assorted Spare Parts",
                $"{npcName} hands you a crate containing an alarming variety of hardware.",
                $"Deliver the Assorted Spare Parts to {destinationName}. Nobody knows exactly which piece they need yet."
            );
        }

        // -------------------------------------------------
        // SHADY
        // -------------------------------------------------

        if (profile.primaryFaction == NPCFaction.Shady)
        {
            int shadyRoll = rng.Next(0, 8);

            if (shadyRoll == 0)
            {
                return Make(
                    "No Questions Crate",
                    "Sealed Contraband",
                    $"{npcName} wants a crate moved quietly. Very quietly.",
                    $"Deliver the Sealed Contraband to {destinationName}. {npcName} says it is legal in at least one jurisdiction."
                );
            }

            if (shadyRoll == 1)
            {
                return Make(
                    "Proof of Work",
                    "Bandit's Head",
                    $"{npcName} places a sealed cooler in your cargo bay and advises against opening it.",
                    $"Deliver the Bandit's Head to {destinationName}. Someone apparently wants proof that a contract was completed."
                );
            }

            if (shadyRoll == 2)
            {
                return Make(
                    "Handle With Gloves",
                    "Severed Hand",
                    $"{npcName} needs a preserved hand delivered with surprisingly little explanation.",
                    $"Deliver the Severed Hand to {destinationName}. {npcName} claims it is for \"biometric purposes.\""
                );
            }

            if (shadyRoll == 3)
            {
                return Make(
                    "Half the Problem",
                    "Half of a Nuclear Warhead",
                    $"{npcName} has half of something you definitely wish was less radioactive.",
                    $"Deliver Half of a Nuclear Warhead to {destinationName}. {npcName} assures you the other half is \"somewhere safe.\""
                );
            }

            if (shadyRoll == 4)
            {
                return Make(
                    "Burn After Delivery",
                    "Encrypted Data Drive",
                    $"{npcName} hands you a data drive and immediately forgets your name.",
                    $"Deliver the Encrypted Data Drive to {destinationName}. Nobody involved wants a receipt."
                );
            }

            if (shadyRoll == 5)
            {
                return Make(
                    "Unregistered Merchandise",
                    "Black-Market Implants",
                    $"{npcName} needs a case of unregistered cybernetics moved off the books.",
                    $"Deliver the Black-Market Implants to {destinationName}. Serial numbers have been removed with impressive enthusiasm."
                );
            }

            if (shadyRoll == 6)
            {
                return Make(
                    "Someone Else's Problem",
                    "Stolen Security Drone",
                    $"{npcName} has acquired a security drone through questionable circumstances.",
                    $"Deliver the Stolen Security Drone to {destinationName}. It keeps trying to report itself missing."
                );
            }

            return Make(
                "Quiet Disposal",
                "Unmarked Evidence Box",
                $"{npcName} needs an evidence box moved somewhere evidence boxes normally do not go.",
                $"Deliver the Unmarked Evidence Box to {destinationName}. The original label has been scratched off."
            );
        }

        // -------------------------------------------------
        // SCIENTIST
        // -------------------------------------------------

        if (profile.primaryFaction == NPCFaction.Scientist)
        {
            int scientistRoll = rng.Next(0, 9);

            if (scientistRoll == 0)
            {
                return Make(
                    "Prototype Handling",
                    "Volatile Prototype",
                    $"{npcName} needs a prototype delivered with most of reality intact.",
                    $"Deliver the Volatile Prototype to {destinationName}. Mild humming is normal. Screaming is less ideal."
                );
            }

            if (scientistRoll == 1)
            {
                return Make(
                    "Cold Chain",
                    "Medical Heart",
                    $"{npcName} needs a transplant organ moved before the preservation window closes.",
                    $"Deliver the Medical Heart to {destinationName}. The container is stable, but the patient cannot wait forever."
                );
            }

            if (scientistRoll == 2)
            {
                return Make(
                    "Fresh Samples",
                    "Research Samples",
                    $"{npcName} needs biological samples transferred to another lab.",
                    $"Deliver the Research Samples to {destinationName}. Keep the container sealed and preferably upright."
                );
            }

            if (scientistRoll == 3)
            {
                return Make(
                    "Do Not Shake",
                    "Antimatter Capsule",
                    $"{npcName} hands you a containment vessel surrounded by several warning labels.",
                    $"Deliver the Antimatter Capsule to {destinationName}. {npcName} strongly recommends smooth flying."
                );
            }

            if (scientistRoll == 4)
            {
                return Make(
                    "Second Opinion",
                    "Preserved Brain",
                    $"{npcName} needs a preserved brain delivered to a specialist.",
                    $"Deliver the Preserved Brain to {destinationName}. Its former owner signed the paperwork, allegedly."
                );
            }

            if (scientistRoll == 5)
            {
                return Make(
                    "Possible Breakthrough",
                    "Alien Tissue Sample",
                    $"{npcName} has a specimen nobody in the lab can confidently identify.",
                    $"Deliver the Alien Tissue Sample to {destinationName}. It has grown slightly since being packaged."
                );
            }

            if (scientistRoll == 6)
            {
                return Make(
                    "Critical Containment",
                    "Leaking Fusion Reactor",
                    $"{npcName} needs a damaged reactor transported before its containment completely fails.",
                    $"Deliver the Leaking Fusion Reactor to {destinationName}. Its energy leakage is continuously overcharging your propulsion systems.",
                    CargoEffectType.OverchargedBoost
                );
            }

            if (scientistRoll == 7)
            {
                return Make(
                    "Mass Reduction Trial",
                    "Experimental Mass Reducer",
                    $"{npcName} presents a prototype that appears to weigh considerably less than it should.",
                    $"Deliver the Experimental Mass Reducer to {destinationName}. Its active mass-reduction field may drastically improve your ship's responsiveness.",
                    CargoEffectType.UltraLight
                );
            }

            return Make(
                "Calibration Required",
                "Quantum Sensor",
                $"{npcName} needs a sensitive research instrument transferred for calibration.",
                $"Deliver the Quantum Sensor to {destinationName}. {npcName} says dropping it would invalidate several years of work."
            );
        }

        // -------------------------------------------------
        // FRIENDLY
        // -------------------------------------------------

        if (profile.primaryFaction == NPCFaction.Friendly)
        {
            int friendlyRoll = rng.Next(0, 6);

            if (friendlyRoll == 0)
            {
                return Make(
                    "Community Favor",
                    "Care Package",
                    $"{npcName} packed a care bundle for someone who needs it.",
                    $"Deliver the Care Package to {destinationName}. {npcName} already tipped because they \"believe in tipping before the disaster.\""
                );
            }

            if (friendlyRoll == 1)
            {
                return Make(
                    "Birthday Run",
                    "Birthday Cake",
                    $"{npcName} needs a cake delivered before the surprise stops being a surprise.",
                    $"Deliver the Birthday Cake to {destinationName}. Aggressive maneuvering is discouraged for obvious reasons."
                );
            }

            if (friendlyRoll == 2)
            {
                return Make(
                    "Missing Home",
                    "Home-Cooked Meal",
                    $"{npcName} cooked something familiar for a friend stationed far from home.",
                    $"Deliver the Home-Cooked Meal to {destinationName}. It smells significantly better than standard station food."
                );
            }

            if (friendlyRoll == 3)
            {
                return Make(
                    "Welcome Aboard",
                    "New Hire Supplies",
                    $"{npcName} put together a starter kit for someone arriving at a new posting.",
                    $"Deliver the New Hire Supplies to {destinationName}. It includes tools, snacks, and an unnecessarily cheerful mug."
                );
            }

            if (friendlyRoll == 4)
            {
                return Make(
                    "Little Emergency",
                    "Child's Medicine",
                    $"{npcName} needs medicine delivered to a family at another station.",
                    $"Deliver the Child's Medicine to {destinationName}. Nothing dramatic—just something important enough not to delay."
                );
            }

            return Make(
                "Special Delivery",
                "Adopted Station Cat",
                $"{npcName} has found a permanent home for a remarkably unimpressed cat.",
                $"Deliver the Adopted Station Cat to {destinationName}. The carrier is secure. Your dignity may not be."
            );
        }

        // -------------------------------------------------
        // SCRAPPER
        // -------------------------------------------------

        if (profile.primaryFaction == NPCFaction.Scrapper)
        {
            int scrapperRoll = rng.Next(0, 4);

            if (scrapperRoll == 0)
            {
                return Make(
                    "Scrap Metal Mountain",
                    "Compressed Scrap Block",
                    $"{npcName} has compressed several tons of salvaged metal into one extremely inconvenient package.",
                    $"Deliver the Compressed Scrap Block to {destinationName}. Don't expect your ship to handle the additional weight gracefully.",
                    CargoEffectType.SuperHeavy
                );
            }

            if (scrapperRoll == 1)
            {
                return Make(
                    "Salvaged and Sparking",
                    "Unstable Salvage Battery",
                    $"{npcName} recovered an old battery that refuses to stop producing energy.",
                    $"Deliver the Unstable Salvage Battery to {destinationName}. Its exposed connections are overloading your ship's boost systems.",
                    CargoEffectType.OverchargedBoost
                );
            }

            if (scrapperRoll == 2)
            {
                return Make(
                    "Parts Worth Saving",
                    "Recovered Engine Parts",
                    $"{npcName} has sorted through a wreck and salvaged a few components worth keeping.",
                    $"Deliver the Recovered Engine Parts to {destinationName}. {npcName} would prefer they arrive in better shape than the original ship."
                );
            }

            return Make(
                "One Ship's Trash",
                "Salvaged Circuit Boards",
                $"{npcName} found a stack of circuit boards that might still be useful to someone.",
                $"Deliver the Salvaged Circuit Boards to {destinationName}. Their previous owners aren't asking for them back."
            );
        }

        // -------------------------------------------------
        // MERCHANT
        // -------------------------------------------------

        if (profile.primaryFaction == NPCFaction.Merchant)
        {
            int merchantRoll = rng.Next(0, 3);

            if (merchantRoll == 0)
            {
                return Make(
                    "Bulk Freight Contract",
                    "Industrial Freight Container",
                    $"{npcName} needs an oversized commercial shipment moved without paying for a dedicated freighter.",
                    $"Deliver the Industrial Freight Container to {destinationName}. The cargo's excessive mass will noticeably impair your ship's handling.",
                    CargoEffectType.SuperHeavy
                );
            }

            if (merchantRoll == 1)
            {
                return Make(
                    "Restock the Shelves",
                    "Retail Supply Crates",
                    $"{npcName} has a shipment of retail stock that needs to reach another station.",
                    $"Deliver the Retail Supply Crates to {destinationName}. {npcName} insists these are the season's most popular items."
                );
            }

            return Make(
                "Invoice Included",
                "Trade Goods",
                $"{npcName} needs a routine shipment moved before the next trade cycle.",
                $"Deliver the Trade Goods to {destinationName}. The paperwork has more pages than the cargo has boxes."
            );
        }

        // -------------------------------------------------
        // ARCHIVIST
        // -------------------------------------------------

        if (profile.primaryFaction == NPCFaction.Archivist)
        {
            int archivistRoll = rng.Next(0, 3);

            if (archivistRoll == 0)
            {
                return Make(
                    "Weight of History",
                    "Ancient Gravity Relic",
                    $"{npcName} has acquired an ancient artifact that seems to exert its own gravitational pull.",
                    $"Deliver the Ancient Gravity Relic to {destinationName}. Its unusual gravitational field dramatically increases your ship's effective mass.",
                    CargoEffectType.SuperHeavy
                );
            }

            if (archivistRoll == 1)
            {
                return Make(
                    "Lost Records",
                    "Historical Data Archive",
                    $"{npcName} recovered a long-missing collection of station records.",
                    $"Deliver the Historical Data Archive to {destinationName}. {npcName} asks you to avoid rewriting history on the way."
                );
            }

            return Make(
                "Handle With History",
                "Preserved Star Charts",
                $"{npcName} needs a fragile set of original star charts transferred for restoration.",
                $"Deliver the Preserved Star Charts to {destinationName}. The routes are outdated, but the originals are irreplaceable."
            );
        }

        // -------------------------------------------------
        // GENERIC DELIVERY POOL
        // -------------------------------------------------

        int genericRoll = rng.Next(0, 8);

        if (genericRoll == 0)
        {
            return Make(
                "Station Errand",
                "Sealed Crate",
                $"{npcName} needs a routine cargo shipment moved.",
                $"Deliver the Sealed Crate to {destinationName}. Simple job, simple pay, theoretically simple trip."
            );
        }

        if (genericRoll == 1)
        {
            return Make(
                "Priority Courier",
                "Priority Package",
                $"{npcName} needs a time-sensitive package delivered.",
                $"Deliver the Priority Package to {destinationName}. Nobody explained why it is urgent, only that it is."
            );
        }

        if (genericRoll == 2)
        {
            return Make(
                "Replacement Parts",
                "Equipment Parts",
                $"{npcName} needs replacement equipment sent to another station.",
                $"Deliver the Equipment Parts to {destinationName}. Someone over there is currently waiting with a wrench."
            );
        }

        if (genericRoll == 3)
        {
            return Make(
                "Paper Trail",
                "Signed Documents",
                $"{npcName} needs physical paperwork transferred for reasons nobody fully understands.",
                $"Deliver the Signed Documents to {destinationName}. Humanity conquered space but apparently not forms."
            );
        }

        if (genericRoll == 4)
        {
            return Make(
                "Restock Run",
                "Food Supplies",
                $"{npcName} needs basic food supplies moved to another station.",
                $"Deliver the Food Supplies to {destinationName}. Mostly practical. Several boxes are exclusively instant noodles."
            );
        }

        if (genericRoll == 5)
        {
            return Make(
                "Personal Effects",
                "Personal Belongings",
                $"{npcName} needs someone's belongings transferred to a new station.",
                $"Deliver the Personal Belongings to {destinationName}. The box is labeled fragile in three different handwriting styles."
            );
        }

        if (genericRoll == 6)
        {
            return Make(
                "Express Shipping",
                "Replacement Uniform",
                $"{npcName} needs a uniform delivered before someone's next shift.",
                $"Deliver the Replacement Uniform to {destinationName}. Apparently showing up in pajamas twice becomes a disciplinary issue."
            );
        }

        return Make(
            "Oddly Specific Errand",
            "Box of Batteries",
            $"{npcName} needs an unreasonable number of batteries delivered.",
            $"Deliver the Box of Batteries to {destinationName}. Nobody is willing to explain what requires this many."
        );
    }

    private static QuestFlavor Make(
        string title,
        string item,
        string shortDesc,
        string fullDesc,
        CargoEffectType cargoEffectType = CargoEffectType.None)
    {
        return new QuestFlavor
        {
            questTitle = "- " + title,
            deliveryItemName = item,
            shortDescription = shortDesc,
            fullDescription = fullDesc,
            cargoEffectType = cargoEffectType
        };
    }

    private static bool Has(NPCProfile profile, NPCPersonalityTrait trait)
    {
        if (profile.personalityTraits == null)
            return false;

        for (int i = 0; i < profile.personalityTraits.Length; i++)
        {
            if (profile.personalityTraits[i] == trait)
                return true;
        }

        return false;
    }

    private static string Pick(System.Random rng, params string[] values)
    {
        return values == null || values.Length == 0
            ? ""
            : values[rng.Next(values.Length)];
    }
}