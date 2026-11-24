namespace BoltCat.Core;

public record RigState(
    long FrequencyHz,
    string Mode,
    bool Ptt,
    int SMeter
);
