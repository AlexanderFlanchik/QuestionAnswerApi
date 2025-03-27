async function handle() {
    const answerId = parameters["answerId"];
    
    await remove("answers", { answerId: answerId });
    
    const questionsFilter = { "answers": { $in: [answerId] } };
    const questionsData = await query("questions", questionsFilter) ;
    const questions = questionsData ? JSON.parse(questionsData) : [];
    
    for (const q of questions) {
        log('Updating question: ' + q["questionId"]);
        let qAnswers = q.answers;
        if (!qAnswers) {
            log('No answers node!');
            continue;
        }
        
        qAnswers = qAnswers.filter(a => a !== answerId);
        await update("questions", { questionId: q["questionId"]}, { answers: qAnswers });
    }
    
    callback(true, null);
}

handle();