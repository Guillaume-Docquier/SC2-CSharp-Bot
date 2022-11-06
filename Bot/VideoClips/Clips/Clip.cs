﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.VideoClips.Manim.Animations;

namespace Bot.VideoClips.Clips;

public abstract class Clip {
    private readonly List<Animation> _animations = new List<Animation>();
    private int _clipFrame = 0;
    private readonly Animation _pauseAtEndOfClipAnimation;

    public bool IsDone { get; private set; } = false;

    protected Clip(int pauseAtEndOfClipDurationSeconds) {
        _pauseAtEndOfClipAnimation = new PauseAnimation(startFrame: 0).WithDurationInSeconds(pauseAtEndOfClipDurationSeconds);
        _animations.Add(_pauseAtEndOfClipAnimation);
    }

    protected void AddAnimation(Animation animation) {
        _animations.Add(animation);

        if (animation.EndFrame > _pauseAtEndOfClipAnimation.StartFrame) {
            _pauseAtEndOfClipAnimation.ChangeStartFrame(animation.EndFrame);
        }
    }

    public async Task Render() {
        if (IsDone) {
            return;
        }

        foreach (var animation in _animations) {
            await animation.Render(_clipFrame);
        }

        _clipFrame++;

        if (_animations.All(animation => animation.EndFrame <= _clipFrame)) {
            IsDone = true;
        }
    }

    public void Reset() {
        _clipFrame = 0;
        IsDone = false;
    }
}
