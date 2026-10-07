import React, { useState } from 'react';

const TeacherGradebook = () => {
    const [quiz, setQuiz] = useState(0);
    const [test, setTest] = useState(0);
    const [mid, setMid] = useState(0);
    const [final, setFinal] = useState(0);
    const [total, setTotal] = useState(0);

    const calculateTotal = () => {
        const score = quiz + test + mid + final;
        setTotal(score);
    };

    return (
        <div>
            <h2>Teacher Gradebook</h2>
            <div>
                <label>Quiz (10): </label>
                <input type="number" value={quiz} onChange={(e) => setQuiz(Number(e.target.value))} />
            </div>
            <div>
                <label>Test (10): </label>
                <input type="number" value={test} onChange={(e) => setTest(Number(e.target.value))} />
            </div>
            <div>
                <label>Midterm (30): </label>
                <input type="number" value={mid} onChange={(e) => setMid(Number(e.target.value))} />
            </div>
            <div>
                <label>Final (50): </label>
                <input type="number" value={final} onChange={(e) => setFinal(Number(e.target.value))} />
            </div>
            <button onClick={calculateTotal}>Calculate Total</button>
            <p>Total: {total}</p>
        </div>
    );
};

export default TeacherGradebook;