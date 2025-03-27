async function handle() {
    const questionId = parameters["questionId"];
    const stages = {
        $match: {
            questionId: questionId
        },
    
        $group: {
            _id: "$questionId",

            totalCount: {
                $count: {}
            }
        },
        
        $project: {
            _id: 0,
            totalCount: 1
        }
    };
    
    const data = await aggregate("answers", stages);
    callback(data, null);
}

handle();