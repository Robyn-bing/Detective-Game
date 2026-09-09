VAR asked_relationship = false
VAR asked_schedule = false
VAR asked_suspects = false

-> start

=== start
123 # speaker:character.clara_remington
123 # speaker:character.margaret_hartley # discover:character.margaret_hartley # relationship:relationship.edward_margaret.spouse
123 # speaker:character.clara_remington
123 # speaker:character.margaret_hartley
-> questions

=== questions
+ [询问和死者的关系]
    123 # speaker:character.clara_remington
    123 # speaker:character.margaret_hartley # unlock:testimony.margaret.relationship.good # unlock:testimony.margaret.relationship.friction
    ~ asked_relationship = true
    -> questions

+ [询问昨日行程]
    123 # speaker:character.clara_remington
    123 # speaker:character.margaret_hartley # unlock:testimony.margaret.action.arrive_study_1900 # unlock:testimony.margaret.action.deliver_dinner
    123 # speaker:character.margaret_hartley # unlock:testimony.margaret.action.leave_study_1915 # unlock:testimony.margaret.action.find_body_0800 # unlock_time:time_period.study_1900_1915
    ~ asked_schedule = true
    -> questions

+ [询问怀疑对象]
    123 # speaker:character.clara_remington
    123 # speaker:character.margaret_hartley # discover:character.james_whitmore # relationship:relationship.edward_james.partner
    123 # speaker:character.margaret_hartley # discover:character.peter_collins # relationship:relationship.edward_peter.superior
    ~ asked_suspects = true
    -> questions

+ { asked_relationship && asked_schedule && asked_suspects } [结束询问]
    -> ending

=== ending
123 # speaker:character.clara_remington
123 # speaker:character.margaret_hartley
-> END
