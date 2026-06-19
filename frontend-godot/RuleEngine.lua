-- MemeAim AI - Dynamic Rule Evaluation Engine
-- Written in Lua for dynamic modding and hot-reloading rules

local RuleEngine = {}

-- Evaluates if a click on a target is valid based on the active rule and the target's tags
-- Returns is_valid (boolean), score_multiplier (number)
function RuleEngine.evaluate_hit(rule_type, target_tags)
    local is_valid = false
    local multiplier = 1.0

    -- Convert target_tags table from C# JSON mapping
    if rule_type == "shoot_enemy" then
        if target_tags["enemy"] == true then
            is_valid = true
            multiplier = 1.0
        else
            is_valid = false
            multiplier = -5.0 -- Penalize shooting civilians/friendly
        end

    elseif rule_type == "avoid_animals" then
        if target_tags["animal"] == true then
            is_valid = false
            multiplier = -3.0 -- Animal protection penalty
        else
            is_valid = true
            -- Extra bonus for human/other enemies
            if target_tags["enemy"] == true then
                multiplier = 1.2
            else
                multiplier = 1.0
            end
        end

    elseif rule_type == "shoot_dancing" then
        if target_tags["dancing"] == true then
            is_valid = true
            multiplier = 1.5 -- Moving/dancing targets worth more!
        else
            is_valid = false
            multiplier = -1.0
        end

    elseif rule_type == "avoid_dancing" then
        if target_tags["dancing"] == true then
            is_valid = false
            multiplier = -2.0 -- Avoid hyperactive targets
        else
            is_valid = true
            multiplier = 1.0
        end

    elseif rule_type == "shoot_hats" then
        if target_tags["hat"] == true then
            is_valid = true
            multiplier = 2.0 -- Rare condition reward!
        else
            is_valid = false
            multiplier = -0.5
        end

    else
        -- Default fallback (shoot anything)
        is_valid = true
        multiplier = 1.0
    end

    return is_valid, multiplier
end

-- Export the engine functions
return RuleEngine
