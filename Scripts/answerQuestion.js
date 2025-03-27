async function handle() {
    var questionId = parameters["questionId"];
    var params = { questionId: questionId };

    var questionData = await query("questions", params);
    if (!questionData) {
        callback(null, "No questions found");
        return;
    }
    
    var answerContent = parameters["answer"];
    if (!answerContent) {
        callback(null, "No answer passed");
        return;
    }
    
    var answer = { answer: answerContent, answerId: newId(), questionId: questionId };
    await insert("answers", answer);
    
    var foundQuestions = JSON.parse(questionData);
    
    var question = foundQuestions[0];
    if (!question) {
        callback(null, "No question found");
        return;
    }
    
    var questionAnswers = [];
    if (question["answers"]) {
        questionAnswers = question["answers"];
    }
    
    questionAnswers.push(answer.answerId);
    
    await update(
        "questions", 
        { "questionId": questionId }, 
        { "answers": questionAnswers }
    );
    
    callback(true, null);
}

handle();