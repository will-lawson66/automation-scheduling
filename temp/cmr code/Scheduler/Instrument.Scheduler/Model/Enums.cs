namespace Instrument.Scheduler.Model;

public enum TubeType
{
    TUBE_TYPE_0 = 0,            // Tube type 0 (e.g. Normal tube)
    TUBE_TYPE_1 = 1,            // Tube type 1 (e.g. Pediatric tube)
    TUBE_TYPE_2 = 2,            // Tube type 2 (e.g. QC bottle)
    TUBE_TYPE_3 = 3,            // Tube type 3
    TUBE_TYPE_4 = 4,            // Tube type 4
    VIAL_NORMAL = 5,            // Normal vial
    VIAL_SMALL = 6,             // Vial with well at the top
    STRIP = 10                  // Strip
}

public enum SampleType
{
    Unspecified = 0,            // Unspecified
    Sample = 1,                 // Regular sample
    QC = 2,                     // Quality control sample
    CC = 3,                     // Curve control sample
    CAL = 4,                    // Calibration control

    DarkBlank = 1000,        // Background signal in the flourometer 
    RinseBlank = 1001,        // Measure the signal from rinse bottle
    ReagentBlank = 1002,        // Measure the signal from a mix of development and stop solution
    FlouroC = 1003         // Placeholder for FlouroC, needs to be defined later
}

public enum SampleInputLocation
{
    Automatic = 0, // Automatic selection by SampleType
    Storage = 1    // Sample tube in sample input drawer, internal sample tube storage or sample shuttle
}

public enum Technology
{
    Unspecified = 0,
    ImmunoCap = 1,
    Elia = 2,
    ImmunoCapViewAllergy = 3,
    EliaDualWash = 4,
}
