async function handle() {
    var questionBody = parameters["question"];
    var question = { questionId: newId(), question: questionBody };
    
    await insert("questions", question);
    callback(question.questionId, null);
}

handle();