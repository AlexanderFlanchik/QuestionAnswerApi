async function handle() {
    const questions = await query("questions", null);
    callback(questions, null);
}

handle();