using CLI;

using Transmitter.Comms;

namespace Transmitter.Client;

partial class Program
{
    private static async Task<Profile?> SetupProfile(Profile? oldProfile = null)
    {
        Logger.LogInputError("profile customization has been disabled for this build");

        return oldProfile;
        //ConsoleIO.Initialize();

        //ProfileCustomizeScreen profileCustomizeScreen = new(oldProfile);
        //profileCustomizeScreen.EnableInput();
        //do
        //{
        //    ConsoleIO.ShowScreen(profileCustomizeScreen);
        //    await profileCustomizeScreen.WaitForInvalidationAsync();
        //} while (profileCustomizeScreen.Status is null);
        //profileCustomizeScreen.DisableInput();

        //ConsoleIO.Reset();

        //return profileCustomizeScreen.Profile!;
    }
}
