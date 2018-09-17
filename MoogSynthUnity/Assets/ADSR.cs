//
//  ADRS.h
//
//  Created by Nigel Redmon on 12/18/12.
//  EarLevel Engineering: earlevel.com
//  Copyright 2012 Nigel Redmon
//
//  For a complete explanation of the ADSR envelope generator and code,
//  read the series of articles by the author, starting here:
//  http://www.earlevel.com/main/2013/06/01/envelope-generators/
//
//  License:
//
//  This source code is provided as is, without warranty.
//  You may copy and distribute verbatim copies of this document.
//  You may modify and use this source code to create binary code for your own purposes, free or commercial.
//
//  1.01  2016-01-02  njr   added calcCoef to SetTargetRatio functions that were in the ADSR widget but missing in this code
//  1.02  2017-01-04  njr   in calcCoef, checked for rate 0, to support non-IEEE compliant compilers
//
//  Converted to C# by Jakob Schmid 2018.

using UnityEngine;

class ADSR
{
    private enum envState
    {
        env_idle = 0,
        env_attack,
        env_decay,
        env_sustain,
        env_release
    };

    private envState state;
    private float output;
    private float attackRate;
    private float decayRate;
    private float releaseRate;
    private float attackCoef;
    private float decayCoef;
    private float releaseCoef;
    private float sustainLevel;
    private float targetRatioA;
    private float targetRatioDR;
    private float attackBase;
    private float decayBase;
    private float releaseBase;

    public ADSR()
    {
        reset();
        setAttackRate(0);
        setDecayRate(0);
        setReleaseRate(0);
        setSustainLevel(1.0f);
        setTargetRatioA(0.3f);
        setTargetRatioDR(0.0001f);
    }

    public void setAttackRate(float rate)
    {
        attackRate = rate;
        attackCoef = calcCoef(rate, targetRatioA);
        attackBase = (1.0f + targetRatioA) * (1.0f - attackCoef);
    }
    public void setDecayRate(float rate)
    {
        decayRate = rate;
        decayCoef = calcCoef(rate, targetRatioDR);
        decayBase = (sustainLevel - targetRatioDR) * (1.0f - decayCoef);
    }
    public void setReleaseRate(float rate)
    {
        releaseRate = rate;
        releaseCoef = calcCoef(rate, targetRatioDR);
        releaseBase = -targetRatioDR * (1.0f - releaseCoef);
    }
    public void setSustainLevel(float level)
    {
        sustainLevel = level;
        decayBase = (sustainLevel - targetRatioDR) * (1.0f - decayCoef);
    }
    public void setTargetRatioA(float targetRatio)
    {
        if (targetRatio < 0.000000001f)
            targetRatio = 0.000000001f;  // -180 dB
        targetRatioA = targetRatio;
        attackCoef = calcCoef(attackRate, targetRatioA);
        attackBase = (1.0f + targetRatioA) * (1.0f - attackCoef);
    }
    public void setTargetRatioDR(float targetRatio)
    {
        if (targetRatio < 0.000000001f)
            targetRatio = 0.000000001f;  // -180 dB
        targetRatioDR = targetRatio;
        decayCoef = calcCoef(decayRate, targetRatioDR);
        releaseCoef = calcCoef(releaseRate, targetRatioDR);
        decayBase = (sustainLevel - targetRatioDR) * (1.0f - decayCoef);
        releaseBase = -targetRatioDR * (1.0f - releaseCoef);
    }
    public void reset()
    {
        state = envState.env_idle;
        output = 0.0f;
    }

    private float calcCoef(float rate, float targetRatio)
    {
        return (rate <= 0) ? 0 : Mathf.Exp(-Mathf.Log((1.0f + targetRatio) / targetRatio) / rate);
    }
    public float process()
    {
        switch (state)
        {
            case envState.env_idle:
                break;
            case envState.env_attack:
                output = attackBase + output * attackCoef;
                if (output >= 1.0f)
                {
                    output = 1.0f;
                    state = envState.env_decay;
                }
                break;
            case envState.env_decay:
                output = decayBase + output * decayCoef;
                if (output <= sustainLevel)
                {
                    output = sustainLevel;
                    state = envState.env_sustain;
                }
                break;
            case envState.env_sustain:
                break;
            case envState.env_release:
                output = releaseBase + output * releaseCoef;
                if (output <= 0.0f)
                {
                    output = 0.0f;
                    state = envState.env_idle;
                }
                break;
        }
        return output;
    }

    public void gate(bool gate)
    {
        if (gate)
            state = envState.env_attack;
        else if (state != envState.env_idle)
            state = envState.env_release;
    }

    envState getState()
    {
        return state;
    }

    float getOutput()
    {
        return output;
    }
}

