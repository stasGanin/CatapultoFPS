using System;

/// <summary>Short on-screen notices ("Recipe learned: …") so gameplay code never references HUD.</summary>
public static class GameMessages
{
    public static event Action<string> Posted;

    public static void Post(string text) => Posted?.Invoke(text);
}
