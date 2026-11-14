// Task L02: the iPhone's own haptic feels for Lines (called from LinesHaptics.cs). The numbers match the
// LinesFeel enum: 0 tick, 1 clear, 2 big clear, 3 error, 4 success, 5 game over.
#import <UIKit/UIKit.h>

static void LinesImpact(UIImpactFeedbackStyle style, CGFloat intensity)
{
    UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
    [generator impactOccurredWithIntensity:intensity];
}

static void LinesNotify(UINotificationFeedbackType type)
{
    UINotificationFeedbackGenerator *generator = [[UINotificationFeedbackGenerator alloc] init];
    [generator notificationOccurred:type];
}

extern "C" void LinesHaptics_Play(int feel)
{
    switch (feel)
    {
        case 0: [[[UISelectionFeedbackGenerator alloc] init] selectionChanged]; break;
        case 1: LinesImpact(UIImpactFeedbackStyleMedium, 1.0); break;
        case 2: LinesImpact(UIImpactFeedbackStyleHeavy, 0.85); break;
        case 3: LinesNotify(UINotificationFeedbackTypeError); break;
        case 4: LinesNotify(UINotificationFeedbackTypeSuccess); break;
        case 5: LinesImpact(UIImpactFeedbackStyleHeavy, 1.0); break;
    }
}
