namespace DetectiveGame.Investigation
{
    /// <summary>Save-safe identifiers shared by Ink, investigation data, recall and UI.</summary>
    public static class InvestigationIds
    {
        public static class Characters
        {
            public const string Edward = "character.edward_hartley";
            public const string Margaret = "character.margaret_hartley";
            public const string James = "character.james_whitmore";
            public const string Peter = "character.peter_collins";
            public const string Clara = "character.clara_remington";
        }

        public static class Relationships
        {
            public const string EdwardMargaretSpouse = "relationship.edward_margaret.spouse";
            public const string EdwardJamesPartner = "relationship.edward_james.partner";
            public const string EdwardPeterSuperior = "relationship.edward_peter.superior";
        }

        public static class Testimonies
        {
            public const string MargaretRelationshipGood = "testimony.margaret.relationship.good";
            public const string MargaretRelationshipFriction = "testimony.margaret.relationship.friction";
            public const string MargaretArriveStudy1900 = "testimony.margaret.action.arrive_study_1900";
            public const string MargaretDeliverDinner = "testimony.margaret.action.deliver_dinner";
            public const string MargaretLeaveStudy1915 = "testimony.margaret.action.leave_study_1915";
            public const string MargaretFindBody0800 = "testimony.margaret.action.find_body_0800";
        }

        public static class TimePeriods
        {
            public const string Study1900To1915 = "time_period.study_1900_1915";
        }
    }
}
