using UnityEngine;
using System.Collections;

public static class Globals
{
    public static MonoSingleton<World> world = new MonoSingleton<World>(false);
}

public static class Settings
{
}
