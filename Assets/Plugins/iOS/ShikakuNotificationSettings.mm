#import <UIKit/UIKit.h>

extern "C" void Shikaku_OpenNotificationSettings()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        NSURL *url = nil;

        if (@available(iOS 15.4, *))
        {
            url = [NSURL URLWithString:
                UIApplicationOpenNotificationSettingsURLString];
        }
        else
        {
            url = [NSURL URLWithString:
                UIApplicationOpenSettingsURLString];
        }

        if (url != nil)
        {
            [UIApplication.sharedApplication
                openURL:url
                options:@{}
                completionHandler:nil];
        }
    });
}
