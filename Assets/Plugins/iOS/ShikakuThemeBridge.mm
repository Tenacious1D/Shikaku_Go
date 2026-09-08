#import <UIKit/UIKit.h>

extern "C" int Shikaku_IsSystemDarkMode()
{
    if (@available(iOS 13.0, *))
    {
        UITraitCollection *traits = UIScreen.mainScreen.traitCollection;

        for (UIScene *scene in UIApplication.sharedApplication.connectedScenes)
        {
            if (scene.activationState != UISceneActivationStateUnattached &&
                [scene isKindOfClass:[UIWindowScene class]])
            {
                UIWindowScene *windowScene = (UIWindowScene *)scene;
                UIWindow *window = windowScene.windows.firstObject;
                if (window != nil)
                {
                    traits = window.traitCollection;
                    break;
                }
            }
        }

        return traits.userInterfaceStyle == UIUserInterfaceStyleDark ? 1 : 0;
    }

    return 0;
}
