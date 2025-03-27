async function handle() {
    const questionId = parameters["questionId"];
    
    await remove("questions", { "questionId": questionId });
    await remove("answers", { "questionId": questionId });
    
    callback(true, null);
}

handle();