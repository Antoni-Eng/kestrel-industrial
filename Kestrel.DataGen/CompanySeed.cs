using Kestrel.Core.Models;

namespace Kestrel.DataGen;

public static class CompanySeed
{
    public static readonly Plant[] Plants =
    [
        new("PLANT-NORTH", "Kestrel North Fabrication", "Sheffield, UK"),
        new("PLANT-SOUTH", "Kestrel South Assembly", "Coventry, UK")
    ];

    public static readonly Equipment[] Equipment =
    [
        new("CNC-204", "Haas VF-2 Vertical Mill", "VF-2", "CNC Mill", "PLANT-NORTH", "Bay A"),
        new("CNC-118", "DMG Mori NLX Lathe", "NLX2500", "CNC Lathe", "PLANT-NORTH", "Bay A"),
        new("CNC-311", "Mazak Variaxis", "j-500", "5-Axis Mill", "PLANT-NORTH", "Bay B"),
        new("PRS-012", "Schuler Hydraulic Press", "H2P-200", "Press", "PLANT-NORTH", "Press Line"),
        new("PRS-045", "AIDA Progressive Press", "NSU-110", "Press", "PLANT-NORTH", "Press Line"),
        new("RBT-07", "Fanuc M-20iD Robot", "M-20iD/25", "Robot", "PLANT-SOUTH", "Cell 1"),
        new("RBT-08", "ABB IRB 6700 Robot", "IRB6700-200", "Robot", "PLANT-SOUTH", "Cell 2"),
        new("CNV-03", "FlexLink Conveyor", "X85", "Conveyor", "PLANT-SOUTH", "Pack Line"),
        new("CNV-11", "Dorner 2200 Conveyor", "2200", "Conveyor", "PLANT-SOUTH", "Pack Line"),
        new("WLD-22", "Lincoln Power Wave Welder", "S500", "Welder", "PLANT-SOUTH", "Weld Bay"),
        new("WLD-23", "Miller Continuum Welder", "500", "Welder", "PLANT-SOUTH", "Weld Bay"),
        new("CMP-01", "Atlas Copco Compressor", "GA37", "Compressor", "PLANT-NORTH", "Utilities"),
        new("HVAC-4", "Trane Rooftop Unit", "IntelliPak", "HVAC", "PLANT-NORTH", "Roof"),
        new("CLN-09", "Ecoclean Parts Washer", "EcoCBase", "Washer", "PLANT-NORTH", "Finishing"),
        new("INS-15", "Zeiss CMM", "CONTURA", "Inspection", "PLANT-SOUTH", "Metrology")
    ];
}
