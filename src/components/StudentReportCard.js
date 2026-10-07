import React from 'react';

const StudentReportCard = ({ totalScore }) => {
    const status = totalScore >= 50 ? 'Pass' : 'Fail';
    const color = status === 'Pass' ? 'green' : 'red';

    return (
        <div>
            <h2>Report Card</h2>
            <p>Total Score: {totalScore}</p>
            <span style={{ color: color }}>{status}</span>
        </div>
    );
};

export default StudentReportCard;