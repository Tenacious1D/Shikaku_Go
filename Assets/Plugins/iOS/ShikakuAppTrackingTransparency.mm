#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>

extern "C" void UnitySendMessage(
    const char *gameObjectName,
    const char *methodName,
    const char *message);

static void ShikakuSendATTResult(
    NSString *gameObjectName,
    NSString *callbackMethodName,
    NSInteger status)
{
    NSString *statusText =
        [NSString stringWithFormat:@"%ld", (long)status];

    UnitySendMessage(
        gameObjectName.UTF8String,
        callbackMethodName.UTF8String,
        statusText.UTF8String);
}

extern "C"
{
    int ShikakuATTGetAuthorizationStatus(void)
    {
        if (@available(iOS 14.0, *))
            return (int)ATTrackingManager.trackingAuthorizationStatus;

        return -1;
    }

    void ShikakuATTRequestAuthorization(
        const char *gameObjectName,
        const char *callbackMethodName)
    {
        NSString *target =
            [NSString stringWithUTF8String:gameObjectName ?: ""];
        NSString *method =
            [NSString stringWithUTF8String:callbackMethodName ?: ""];

        dispatch_async(dispatch_get_main_queue(), ^{
            if (@available(iOS 14.0, *))
            {
                ATTrackingManagerAuthorizationStatus currentStatus =
                    ATTrackingManager.trackingAuthorizationStatus;

                if (currentStatus !=
                    ATTrackingManagerAuthorizationStatusNotDetermined)
                {
                    ShikakuSendATTResult(
                        target,
                        method,
                        currentStatus);
                    return;
                }

                [ATTrackingManager
                    requestTrackingAuthorizationWithCompletionHandler:
                    ^(ATTrackingManagerAuthorizationStatus status)
                    {
                        dispatch_async(dispatch_get_main_queue(), ^{
                            ShikakuSendATTResult(
                                target,
                                method,
                                status);
                        });
                    }];
            }
            else
            {
                ShikakuSendATTResult(target, method, -1);
            }
        });
    }
}
